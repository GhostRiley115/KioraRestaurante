using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace KioraRestaurante.Services.PagamentosOnline;
public class CheckoutOnlineService(AppDbContext db, MpOrdersClient api, IOptions<MercadoPagoConfig> config)
{
    private static string Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) ? v.ToString() : "";
    private static decimal? Valor(JsonElement e, string nome)
    {
        // Ausente ou nulo significa valor não informado; não significa pagamento aprovado.
        if (!e.TryGetProperty(nome, out var campo)
            || campo.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (campo.ValueKind == JsonValueKind.Number
            && campo.TryGetDecimal(out var numero)
            && numero >= 0)
        {
            return numero;
        }

        if (campo.ValueKind == JsonValueKind.String)
        {
            var texto = campo.GetString();

            if (string.IsNullOrWhiteSpace(texto))
                return null;

            if (decimal.TryParse(
                    texto,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var valor)
                && valor >= 0)
            {
                return valor;
            }
        }

        throw new FalhaPagamento(
            $"O Mercado Pago retornou um valor inválido em {nome}. Consulte novamente o pagamento.");
    }

    public async Task<Pedido> ExigirPedido(int usuarioId, int id, CancellationToken ct) =>
        await db.Pedidos.AsNoTracking().Include(p => p.ItensPedido)
            .SingleOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId && p.Usuario.Ativo, ct)
            ?? throw new KeyNotFoundException();

    public async Task<string> Iniciar(int usuarioId, int id, CancellationToken ct)
    {
        var pedido = await ExigirPedido(usuarioId, id, ct);
        if (pedido.FormaPagamento != FormaPagamento.MercadoPago
            || pedido.StatusPedido == StatusPedido.Cancelado
            || pedido.StatusPagamento != StatusPagamento.Pendente)
            throw new FalhaPagamento("Este pedido não está disponível para iniciar um pagamento online.");
        if (pedido.OperacaoPendenteId.HasValue)
            throw new FalhaPagamento("Conclua a alteração pendente do pedido antes de iniciar o pagamento.");
        var o = config.Value;
        if (!o.Ativado || string.IsNullOrWhiteSpace(o.VendedorId) || string.IsNullOrWhiteSpace(o.ApplicationId)
            || !Uri.TryCreate(o.UrlPublica, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
            throw new FalhaPagamento("Confira as configurações do pagamento online.");

        var registro = await db.PagamentosOnline.SingleOrDefaultAsync(p => p.PedidoId == id, ct);
        if (registro == null)
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            var novaVersaoPedido = Guid.NewGuid();
            var atualizados = await db.Pedidos.Where(p => p.Id == id && p.Versao == pedido.Versao
                    && p.StatusPedido == pedido.StatusPedido && p.OperacaoPendenteId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Versao, novaVersaoPedido), ct);
            if (atualizados != 1) throw new FalhaPagamento("O pedido mudou. Consulte os detalhes antes de pagar.");
            // O comprador já é identificado na criação, antes de abrir o checkout.
            // Em teste, não envia o e-mail real do cliente ao ambiente de simulação.
            var emailComprador = o.ModoTeste
                ? (o.EmailCompradorTeste ?? "").Trim()
                : (await db.Usuarios.AsNoTracking()
                    .Where(u => u.Id == usuarioId && u.Ativo)
                    .Select(u => u.Email)
                    .SingleAsync(ct)).Trim();

            if (!new EmailAddressAttribute().IsValid(emailComprador))
                throw new FalhaPagamento("Confira o e-mail do comprador antes de iniciar o pagamento.");

            var referencia = Guid.NewGuid();
            var retorno = o.UrlPublica.TrimEnd('/') + "/Pagamento/Retorno/" + id;
            if (pedido.ValorTotal <= 0 || pedido.TaxaEntrega < 0
                || pedido.DescontoCupom < 0 || pedido.DescontoCupom > pedido.ItensPedido.Sum(i => i.PrecoUnitario * i.Quantidade)
                || pedido.ItensPedido.Sum(i => i.PrecoUnitario * i.Quantidade) - pedido.DescontoCupom + pedido.TaxaEntrega != pedido.ValorTotal)
                throw new FalhaPagamento("O total do pedido precisa ser conferido.");
            var itensPagamento = pedido.ItensPedido.Select(i => new {
                title = i.NomeProduto, quantity = i.Quantidade,
                unit_price = i.PrecoUnitario.ToString("0.00", CultureInfo.InvariantCulture)
            }).ToList();
            // O provedor recebe os produtos com desconto em uma linha, evitando diferenças de centavos por quantidade.
            if (pedido.DescontoCupom > 0)
            {
                itensPagamento.Clear();
                var produtosComDesconto = pedido.ItensPedido.Sum(i => i.Subtotal) - pedido.DescontoCupom;
                if (produtosComDesconto > 0)
                    itensPagamento.Add(new { title = $"Produtos do pedido #{pedido.Id} · cupom {pedido.CodigoCupom}", quantity = 1,
                        unit_price = produtosComDesconto.ToString("0.00", CultureInfo.InvariantCulture) });
            }
            // A entrega compõe o mesmo total conferido e cobrado do pedido.
            if (pedido.TaxaEntrega > 0)
                itensPagamento.Add(new { title = "Taxa de entrega", quantity = 1,
                    unit_price = pedido.TaxaEntrega.ToString("0.00", CultureInfo.InvariantCulture) });
            registro = new PagamentoOnline { PedidoId = id, Referencia = referencia, Valor = pedido.ValorTotal,
                RevisaoFinanceira = pedido.RevisaoFinanceira };
            registro.CorpoCriacao = JsonSerializer.Serialize(new
            {
                type = "online", processing_mode = "manual", capture_mode = "automatic_async",
                total_amount = pedido.ValorTotal.ToString("0.00", CultureInfo.InvariantCulture),
                external_reference = referencia.ToString("N"), expiration_time = "PT30M",
                payer = new { email = emailComprador },
                items = itensPagamento,
                config = new { online = new {
                    success_url = retorno, failure_url = retorno, pending_url = retorno, auto_return = "all"
                }}
            });
            db.PagamentosOnline.Add(registro);
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        if (registro.Valor != pedido.ValorTotal)
            throw new FalhaPagamento("O valor do pedido mudou. Solicite conferência antes de pagar.");
        if (registro.RevisaoNecessaria || registro.CriadoEmUtc < DateTime.UtcNow.AddHours(-12))
            throw new FalhaPagamento("Solicite a conferência deste pagamento ao restaurante antes de tentar novamente.");
        if (registro.OrderId != null)
        {
            await Sincronizar(registro.OrderId, ct);
            db.ChangeTracker.Clear();
            var atual = await ExigirPedido(usuarioId, id, ct);
            if (atual.OperacaoPendenteId.HasValue || atual.ValorTotal != registro.Valor
                || !await db.PagamentosOnline.AsNoTracking().AnyAsync(p => p.PedidoId == id && p.Referencia == registro.Referencia, ct))
                throw new FalhaPagamento("O pedido está sendo alterado. Confira os detalhes antes de pagar.");
            if (atual.StatusPagamento != StatusPagamento.Pendente)
                throw new FalhaPagamento("O pagamento já foi atualizado. Confira o status do pedido.");
            if (DateTime.UtcNow >= registro.CriadoEmUtc.AddMinutes(30))
                throw new FalhaPagamento("A sessão expirou. Confira o pedido com o restaurante.");
            if (MpOrdersClient.UrlValida(registro.CheckoutUrl)) return registro.CheckoutUrl!;
            throw new FalhaPagamento("O link de pagamento precisa ser conferido.");
        }
        // O mesmo corpo e chave são usados após timeout; não abre outra cobrança.
        using var resposta = await api.Enviar(null, registro.CorpoCriacao, registro.Referencia.ToString(), ct);
        var json = resposta.RootElement;
        Conferir(json, registro);
        var orderId = Texto(json, "id");
        var url = Texto(json, "checkout_url");
        if (!MpOrdersClient.IdValido(orderId) || !MpOrdersClient.UrlValida(url))
            throw new FalhaPagamento("O provedor não devolveu um checkout válido.");
        registro.OrderId = orderId;
        registro.CheckoutUrl = url;
        registro.Versao = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var salvo = await db.PagamentosOnline.AsNoTracking().SingleOrDefaultAsync(p => p.PedidoId == id, ct);
            if (salvo == null || salvo.OrderId != orderId) throw new FalhaPagamento("Consulte novamente o pagamento.");
        }
        return url;
    }

    public void Conferir(JsonElement json, PagamentoOnline registro)
    {
        var o = config.Value;
        var divergencias = new List<string>();

        void Verificar(bool correto, string campo)
        {
            if (!correto)
                divergencias.Add(campo);
        }

        Verificar(
            Texto(json, "type") == "online",
            "tipo da order");

        Verificar(
            Texto(json, "processing_mode") == "manual",
            "modo de processamento");

        Verificar(
            Texto(json, "external_reference")
            == registro.Referencia.ToString("N"),
            "referência do pedido");

        Verificar(
            Texto(json, "user_id") == o.VendedorId.Trim(),
            "VendedorId");

        Verificar(
            Texto(json, "currency") == "BRL",
            "moeda");

        Verificar(
            Texto(json, "country_code") == "BRA",
            "país do vendedor");

        var valorValido = decimal.TryParse(
            Texto(json, "total_amount"),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var total);

        Verificar(
            valorValido && total == registro.Valor,
            "valor total");

        var applicationIdRecebido = "";

        if (json.TryGetProperty("integration_data", out var integracao)
            && integracao.ValueKind == JsonValueKind.Object)
        {
            applicationIdRecebido = Texto(integracao, "application_id");
        }

        Verificar(
            applicationIdRecebido == o.ApplicationId.Trim(),
            "ApplicationId");

        var identificadorDeTeste = Texto(json, "id")
            .StartsWith("ORDTST", StringComparison.Ordinal);

        Verificar(
            identificadorDeTeste == o.ModoTeste,
            "ambiente / ModoTeste");

        if (divergencias.Count > 0)
        {
            // Exibe somente os nomes dos campos; não revela credenciais nem valores.
            throw new FalhaPagamento(
                "Dados divergentes na configuração do pagamento: "
                + string.Join(", ", divergencias)
                + ". Confira as configurações ou avise o restaurante.");
        }
    }

    public async Task Sincronizar(string orderId, CancellationToken ct)
    {
        // Status recebido no navegador ou no corpo do webhook nunca aprova o pedido.
        using var resposta = await api.Enviar(orderId, null, null, ct);
        var json = resposta.RootElement;
        if (Texto(json, "id") != orderId) throw new FalhaPagamento("Order divergente.");
        if (!Guid.TryParseExact(Texto(json, "external_reference"), "N", out var referencia)) return;
        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            db.ChangeTracker.Clear();
            var registro = await db.PagamentosOnline.Include(p => p.Pedido)
                .SingleOrDefaultAsync(p => p.Referencia == referencia, ct);
            if (registro == null) return; // Outro fluxo da mesma aplicação, sem pedido local.
            var alteracoes = await db.OperacoesPedido.Where(o => o.PedidoId == registro.PedidoId
                    && o.Tipo == TipoOperacaoPedido.Endereco && o.Estado == EstadoOperacaoPedido.Concluida
                    && o.RevisaoFinanceiraAplicada > registro.RevisaoFinanceira)
                .Select(o => new { o.TaxaAnterior, o.NovaTaxa }).ToListAsync(ct);
            if (registro.Valor + alteracoes.Sum(o => o.NovaTaxa - o.TaxaAnterior) != registro.Pedido.ValorTotal)
                throw new FalhaPagamento("O total local mudou e precisa ser conferido.");
            Conferir(json, registro);
            if (registro.OrderId != null && registro.OrderId != orderId)
                throw new FalhaPagamento("Order diferente da registrada para o pedido.");
            var data = DateTimeOffset.Parse(Texto(json, "last_updated_date"), CultureInfo.InvariantCulture).UtcDateTime;
            if (registro.AtualizacaoExternaUtc.HasValue && data < registro.AtualizacaoExternaUtc.Value) return;
            var status = Texto(json, "status"); var detalhe = Texto(json, "status_detail");
            var pago = Valor(json, "total_paid_amount");
            var proximo = Mapear(status, detalhe, pago, registro.Valor);
            if (status == "processed" && detalhe == "partially_refunded")
            {
                var conhecidos = await db.MovimentosPedido.AsNoTracking().Where(m => m.OrderId == orderId
                    && m.Tipo == TipoMovimentoPedido.Devolucao && m.Estado == EstadoMovimentoPedido.Confirmado)
                    .Select(m => new { m.ReembolsoId, m.Valor, m.TransacaoId }).ToListAsync(ct);
                var externos = DadosOrder.Transacoes(json, "refunds");
                if (conhecidos.Count > 0 && externos.Length == conhecidos.Count
                    && externos.All(r => DadosOrder.Texto(r, "status") == "processed"
                        && conhecidos.Any(c => c.ReembolsoId == DadosOrder.Texto(r, "id")
                            && c.TransacaoId == DadosOrder.Texto(r, "transaction_id") && c.Valor == DadosOrder.Valor(r, "amount")))
                    && (pago == registro.Valor || pago == registro.Valor - conhecidos.Sum(c => c.Valor)))
                    proximo = StatusPagamento.Aprovado;
            }
            registro.OrderId = orderId;
            registro.AtualizacaoExternaUtc = data;
            registro.ConferidoEmUtc = DateTime.UtcNow;
            registro.SituacaoExterna = (status + "/" + detalhe)[..Math.Min(150, (status + "/" + detalhe).Length)];
            registro.RevisaoNecessaria = proximo is StatusPagamento.Conferencia or StatusPagamento.Reembolsado
                || (registro.Pedido.StatusPedido == StatusPedido.Cancelado && proximo == StatusPagamento.Aprovado);
            registro.Pedido.StatusPagamento = proximo;
            if (proximo == StatusPagamento.Reembolsado)
            {
                var extras = await db.MovimentosPedido.AsNoTracking().Where(m => m.Operacao.PedidoId == registro.PedidoId
                    && m.Tipo == TipoMovimentoPedido.Cobranca && m.Estado == EstadoMovimentoPedido.Confirmado).ToListAsync(ct);
                var devolucoesExtras = await db.MovimentosPedido.AsNoTracking().Where(m => m.Operacao.PedidoId == registro.PedidoId
                    && m.Tipo == TipoMovimentoPedido.Devolucao && m.Estado == EstadoMovimentoPedido.Confirmado
                    && m.OrderId != orderId).ToListAsync(ct);
                registro.Pedido.ReembolsoCancelamentoPendente = extras.Sum(m => m.Valor) > devolucoesExtras.Sum(m => m.Valor);
            }
            registro.Versao = Guid.NewGuid();
            try { await db.SaveChangesAsync(ct); return; }
            catch (DbUpdateConcurrencyException) when (tentativa < 2) { }
        }
    }

    public static StatusPagamento Mapear(
        string status,
        string detalhe,
        decimal? pago,
        decimal valor) => (status, detalhe) switch
    {
        ("processed", "accredited")
            when pago.HasValue
                 && pago.Value == valor
                 && valor > 0
            => StatusPagamento.Aprovado,

        ("refunded", _) => StatusPagamento.Reembolsado,

        ("failed", _) => StatusPagamento.Recusado,

        ("canceled", _) => StatusPagamento.Cancelado,

        ("created" or "processing" or "action_required", _)
            when !pago.HasValue || pago.Value == 0
            => StatusPagamento.Pendente,

        // Inclui estorno parcial ou divergência de valor.
        // Uma aprovação externa sem valor informado também exige conferência.
        _ => StatusPagamento.Conferencia
    };
}
