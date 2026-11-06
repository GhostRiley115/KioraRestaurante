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
    private static decimal Valor(JsonElement e, string nome) =>
        decimal.Parse(Texto(e, nome), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

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
        var o = config.Value;
        if (!o.Ativado || string.IsNullOrWhiteSpace(o.VendedorId) || string.IsNullOrWhiteSpace(o.ApplicationId)
            || !Uri.TryCreate(o.UrlPublica, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https")
            throw new FalhaPagamento("Confira as configurações do pagamento online.");

        var registro = await db.PagamentosOnline.SingleOrDefaultAsync(p => p.PedidoId == id, ct);
        if (registro == null)
        {
            var referencia = Guid.NewGuid();
            var retorno = o.UrlPublica.TrimEnd('/') + "/Pagamento/Retorno/" + id;
            if (pedido.ValorTotal <= 0 || pedido.ItensPedido.Sum(i => i.PrecoUnitario * i.Quantidade) != pedido.ValorTotal)
                throw new FalhaPagamento("O total do pedido precisa ser conferido.");
            registro = new PagamentoOnline { PedidoId = id, Referencia = referencia, Valor = pedido.ValorTotal };
            registro.CorpoCriacao = JsonSerializer.Serialize(new
            {
                type = "online", processing_mode = "manual", capture_mode = "automatic_async",
                total_amount = pedido.ValorTotal.ToString("0.00", CultureInfo.InvariantCulture),
                external_reference = referencia.ToString("N"), expiration_time = "PT30M",
                items = pedido.ItensPedido.Select(i => new {
                    title = i.NomeProduto, quantity = i.Quantidade,
                    unit_price = i.PrecoUnitario.ToString("0.00", CultureInfo.InvariantCulture)
                }),
                config = new { online = new {
                    success_url = retorno, failure_url = retorno, pending_url = retorno, auto_return = "all"
                }}
            });
            db.PagamentosOnline.Add(registro);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                registro = await db.PagamentosOnline.SingleOrDefaultAsync(p => p.PedidoId == id, ct);
                if (registro == null) throw;
            }
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
            var salvo = await db.PagamentosOnline.AsNoTracking().SingleAsync(p => p.PedidoId == id, ct);
            if (salvo.OrderId != orderId) throw new FalhaPagamento("Consulte novamente o pagamento.");
        }
        return url;
    }

    private void Conferir(JsonElement json, PagamentoOnline registro)
    {
        var o = config.Value;
        if (Texto(json, "type") != "online" || Texto(json, "processing_mode") != "manual"
            || Texto(json, "external_reference") != registro.Referencia.ToString("N")
            || Texto(json, "user_id") != o.VendedorId || Texto(json, "currency") != "BRL"
            || Texto(json, "country_code") != "BRA" || Valor(json, "total_amount") != registro.Valor
            || Texto(json.GetProperty("integration_data"), "application_id") != o.ApplicationId
            || Texto(json, "id").StartsWith("ORDTST", StringComparison.Ordinal) != o.ModoTeste)
            throw new FalhaPagamento("Os dados do provedor não correspondem ao pedido, vendedor ou ambiente configurado.");
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
            if (registro.Valor != registro.Pedido.ValorTotal)
                throw new FalhaPagamento("O total local mudou e precisa ser conferido.");
            Conferir(json, registro);
            if (registro.OrderId != null && registro.OrderId != orderId)
                throw new FalhaPagamento("Order diferente da registrada para o pedido.");
            var data = DateTimeOffset.Parse(Texto(json, "last_updated_date"), CultureInfo.InvariantCulture).UtcDateTime;
            if (registro.AtualizacaoExternaUtc.HasValue && data <= registro.AtualizacaoExternaUtc.Value) return;
            var status = Texto(json, "status"); var detalhe = Texto(json, "status_detail");
            var pago = Valor(json, "total_paid_amount");
            var proximo = Mapear(status, detalhe, pago, registro.Valor);
            registro.OrderId = orderId;
            registro.AtualizacaoExternaUtc = data;
            registro.ConferidoEmUtc = DateTime.UtcNow;
            registro.SituacaoExterna = (status + "/" + detalhe)[..Math.Min(150, (status + "/" + detalhe).Length)];
            registro.RevisaoNecessaria = proximo is StatusPagamento.Conferencia or StatusPagamento.Reembolsado
                || (registro.Pedido.StatusPedido == StatusPedido.Cancelado && proximo == StatusPagamento.Aprovado);
            registro.Pedido.StatusPagamento = proximo;
            registro.Versao = Guid.NewGuid();
            try { await db.SaveChangesAsync(ct); return; }
            catch (DbUpdateConcurrencyException) when (tentativa < 2) { }
        }
    }

    public static StatusPagamento Mapear(string status, string detalhe, decimal pago, decimal valor) => (status, detalhe) switch
    {
        ("processed", "accredited") when pago == valor => StatusPagamento.Aprovado,
        ("refunded", _) => StatusPagamento.Reembolsado,
        ("failed", _) => StatusPagamento.Recusado,
        ("canceled", _) => StatusPagamento.Cancelado,
        ("created" or "processing" or "action_required", _) when pago == 0 => StatusPagamento.Pendente,
        _ => StatusPagamento.Conferencia // Inclui estorno parcial ou divergência de valor.
    };
}
