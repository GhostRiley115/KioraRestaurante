using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Notificacoes;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Entrega;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services;

public class GerenciarPedidoService(AppDbContext db, EnderecoUsuarioService enderecos, CotacaoEntregaService entrega,
    MpOrdersClient api, CheckoutOnlineService pagamentos, IOptions<MercadoPagoConfig> config, TimeProvider relogio,
    ILogger<GerenciarPedidoService> logger)
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
    private static string Dinheiro(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    public static bool PodeAlterarEndereco(Pedido p) => p.StatusPedido is StatusPedido.Recebido or StatusPedido.EmPreparo;

    public async Task<Pedido> Buscar(int usuarioId, int id, CancellationToken ct = default) =>
        await db.Pedidos.AsNoTracking().Include(p => p.EnderecoEntrega).Include(p => p.ItensPedido)
            .SingleOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId && p.Usuario.Ativo, ct)
        ?? throw new KeyNotFoundException();

    public async Task<CotacaoEntrega> Cotar(int usuarioId, int id, Guid versao, CotarEntregaRequestDTO dto, CancellationToken ct)
    {
        var p = await Buscar(usuarioId, id, ct);
        ExigirEdicao(p, versao);
        var endereco = await enderecos.PrepararParaCheckout(usuarioId, dto);
        return await entrega.Cotar(usuarioId, endereco.Entrega, ct);
    }

    private static void ExigirEdicao(Pedido p, Guid versao)
    {
        if (!PodeAlterarEndereco(p)) throw new ValidationException("Não é possível alterar o endereço: o pedido já saiu para entrega ou foi encerrado.");
        if (p.OperacaoPendenteId.HasValue) throw new ValidationException("Já existe uma alteração em andamento. Confira os detalhes do pedido.");
        if (p.Versao != versao) throw new ValidationException("O pedido mudou. Abra novamente a alteração de endereço.");
    }

    public async Task<Guid> AlterarEndereco(int usuarioId, AlterarEnderecoPedidoRequestDTO dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new(dto), true);
        var p = await Buscar(usuarioId, dto.PedidoId, ct);
        ExigirEdicao(p, dto.VersaoPedido);
        var endereco = await enderecos.PrepararParaCheckout(usuarioId, dto);
        var cotacao = entrega.Conferir(usuarioId, endereco.Entrega, dto.TokenEntrega);
        var novoTotal = p.ItensPedido.Sum(i => i.Subtotal) - p.DescontoCupom + cotacao.Taxa;
        if (novoTotal is <= 0 or > 99999999.99m)
            throw new ValidationException("O novo total ultrapassa o limite permitido. Peça ajuda ao restaurante.");
        var op = new OperacaoPedido
        {
            PedidoId = p.Id, Tipo = TipoOperacaoPedido.Endereco, TaxaAnterior = p.TaxaEntrega, NovaTaxa = cotacao.Taxa,
            DistanciaMetros = cotacao.DistanciaMetros, DuracaoSegundos = cotacao.DuracaoPercursoSegundos,
            EnderecoAnteriorJson = JsonSerializer.Serialize(Copiar(p.EnderecoEntrega)),
            NovoEnderecoJson = JsonSerializer.Serialize(Copiar(endereco.Entrega)),
            EnderecoSalvarJson = endereco.Salvar == null ? null : JsonSerializer.Serialize(endereco.Salvar),
            CriadaEmUtc = Agora, ConferidaEmUtc = Agora
        };
        await Registrar(p, op, dto.VersaoPedido, ct);
        await Processar(op.Id, ct);
        return op.Id;
    }

    public async Task<Guid> Cancelar(int usuarioId, CancelarPedidoRequestDTO dto, CancellationToken ct)
    {
        Validator.ValidateObject(dto, new(dto), true);
        var p = await Buscar(usuarioId, dto.PedidoId, ct);
        if (p.StatusPedido == StatusPedido.Cancelado)
        {
            var anterior = await db.OperacoesPedido.Where(o => o.PedidoId == p.Id && o.Tipo == TipoOperacaoPedido.Cancelamento)
                .OrderByDescending(o => o.CriadaEmUtc).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct);
            return anterior ?? throw new ValidationException("Este pedido já está cancelado.");
        }
        if (p.StatusPedido != StatusPedido.Recebido) throw new ValidationException("O cancelamento pelo site só é permitido enquanto o pedido estiver Recebido, antes do preparo.");
        if (p.OperacaoPendenteId.HasValue) throw new ValidationException("Conclua ou desista da alteração pendente antes de cancelar o pedido.");
        if (p.Versao != dto.VersaoPedido) throw new ValidationException("O pedido mudou. Atualize os detalhes antes de cancelar.");
        var op = new OperacaoPedido { PedidoId = p.Id, Tipo = TipoOperacaoPedido.Cancelamento,
            Motivo = dto.Motivo?.Trim(), CriadaEmUtc = Agora, ConferidaEmUtc = Agora };
        await Registrar(p, op, dto.VersaoPedido, ct);
        await Processar(op.Id, ct);
        return op.Id;
    }

    public async Task<ResultadoCancelamentoAdmin> CancelarPeloAdministrador(int adminId, CancelarPedidoAdminRequestDTO dto, CancellationToken ct = default)
    {
        var admin = await db.Usuarios.AsNoTracking().Where(u => u.Id == adminId && u.Ativo && u.Tipo == TipoUsuario.Administrador)
            .Select(u => new { u.Id, u.Nome }).SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        Validator.ValidateObject(dto, new(dto), true);
        var p = await db.Pedidos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == dto.PedidoId, ct) ?? throw new KeyNotFoundException();
        if (p.StatusPedido == StatusPedido.Cancelado) return new(true, p.ReembolsoCancelamentoPendente);
        if (p.StatusPedido is not (StatusPedido.Recebido or StatusPedido.EmPreparo))
            throw new ValidationException("O administrador só pode cancelar antes de o pedido sair para entrega.");
        if (p.OperacaoPendenteId.HasValue) throw new ValidationException("Há uma solicitação em andamento. Aguarde sua conclusão antes de cancelar.");
        if (p.Versao != dto.VersaoPedido) throw new ValidationException("O pedido mudou. Atualize os detalhes antes de cancelar.");
        var op = new OperacaoPedido {
            PedidoId = p.Id, Tipo = TipoOperacaoPedido.Cancelamento, Motivo = dto.Motivo.Trim(),
            AdministradorSolicitanteId = admin.Id, AdministradorSolicitanteNome = admin.Nome, CriadaEmUtc = Agora, ConferidaEmUtc = Agora
        };
        await Registrar(p, op, dto.VersaoPedido, ct);
        await Processar(op.Id, ct);
        return await db.Pedidos.AsNoTracking().Where(x => x.Id == p.Id)
            .Select(x => new ResultadoCancelamentoAdmin(x.StatusPedido == StatusPedido.Cancelado, x.ReembolsoCancelamentoPendente)).SingleAsync(ct);
    }

    private async Task AvisarCozinhaDoCancelamento(OperacaoPedido op, CancellationToken ct)
    {
        var ids = await db.Usuarios.Where(u => u.Ativo && (u.Tipo == TipoUsuario.Cozinha || u.Tipo == TipoUsuario.Gerente || u.Tipo == TipoUsuario.Administrador))
            .Select(u => u.Id).ToListAsync(ct);
        // O aviso e o cancelamento são gravados juntos; a chave da operação evita duplicação em retentativas.
        var aviso = new Notificacao {
            Id = op.Id, Tipo = TipoNotificacao.CorrecaoCozinha, Publico = PublicoNotificacao.Funcionario,
            AutorId = op.AdministradorSolicitanteId, PedidoId = op.PedidoId, Titulo = $"Delivery #{op.PedidoId}: correção no pedido",
            Mensagem = $"Pedido cancelado pelo restaurante. Interrompa o preparo. Motivo: {op.Motivo}", CriadaEmUtc = Agora
        };
        db.Notificacoes.Add(aviso);
        db.NotificacoesUsuarios.AddRange(ids.Select(u => new NotificacaoUsuario { Notificacao = aviso, UsuarioId = u }));
    }

    private async Task Registrar(Pedido p, OperacaoPedido op, Guid versao, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var novaVersaoPedido = Guid.NewGuid();
        var n = await db.Pedidos.Where(x => x.Id == p.Id && x.UsuarioId == p.UsuarioId && (op.AdministradorSolicitanteId.HasValue
                    ? db.Usuarios.Any(u=>u.Id==op.AdministradorSolicitanteId.Value&&u.Ativo&&u.Tipo==TipoUsuario.Administrador)
                    : x.Usuario.Ativo)
                && x.StatusPedido == p.StatusPedido && x.Versao == versao && x.OperacaoPendenteId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Versao, novaVersaoPedido).SetProperty(x => x.OperacaoPendenteId, (Guid?)op.Id), ct);
        if (n != 1) throw new ValidationException("O pedido mudou durante a confirmação. Atualize os detalhes.");
        db.OperacoesPedido.Add(op);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<OperacaoPedido> BuscarOperacao(int usuarioId, Guid id, CancellationToken ct) =>
        await db.OperacoesPedido.AsNoTracking().Include(o => o.Pedido).Include(o => o.Movimentos)
            .SingleOrDefaultAsync(o => o.Id == id && o.Pedido.UsuarioId == usuarioId && o.Pedido.Usuario.Ativo, ct)
        ?? throw new KeyNotFoundException();

    public async Task Desistir(int usuarioId, Guid id, CancellationToken ct)
    {
        var op = await BuscarOperacao(usuarioId, id, ct);
        if (op.Tipo != TipoOperacaoPedido.Endereco || op.Finalizada) throw new ValidationException("Esta alteração não está disponível para desistência.");
        if (op.Movimentos.Any(m => m.Tipo == TipoMovimentoPedido.Devolucao))
            throw new ValidationException("A devolução já foi solicitada. Aguarde a conferência ou peça ajuda ao restaurante.");
        await db.OperacoesPedido.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.DesistenciaSolicitada, true), ct);
        await Processar(id, ct);
    }

    public async Task<string> LinkPagamento(int usuarioId, Guid id, CancellationToken ct)
    {
        await BuscarOperacao(usuarioId, id, ct);
        await Processar(id, ct);
        var op = await BuscarOperacao(usuarioId, id, ct);
        var movimento = op.Movimentos.SingleOrDefault(m => m.Tipo == TipoMovimentoPedido.Cobranca);
        if (op.Estado != EstadoOperacaoPedido.AguardandoPagamento || movimento == null || !MpOrdersClient.UrlValida(movimento.CheckoutUrl))
            throw new ValidationException("A cobrança não está disponível. Atualize o andamento da alteração.");
        return movimento.CheckoutUrl!;
    }

    public async Task SincronizarOrder(string orderId, CancellationToken ct)
    {
        var ids = await db.MovimentosPedido.AsNoTracking().Where(m => m.OrderId == orderId
            && m.Operacao.Estado != EstadoOperacaoPedido.Concluida && m.Operacao.Estado != EstadoOperacaoPedido.Desfeita)
            .Select(m => m.OperacaoPedidoId).Distinct().ToListAsync(ct);
        foreach (var id in ids) await Processar(id, ct);
    }

    public async Task Processar(Guid id, CancellationToken ct)
    {
        var concessao = Guid.NewGuid();
        // Uma concessão curta evita processadores simultâneos. As chaves externas também são idempotentes.
        var obtida = await db.OperacoesPedido.Where(o => o.Id == id && o.Estado != EstadoOperacaoPedido.Concluida
                && o.Estado != EstadoOperacaoPedido.Desfeita && (o.ProcessandoAteUtc == null || o.ProcessandoAteUtc < Agora))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProcessandoAteUtc, Agora.AddMinutes(2)).SetProperty(o => o.Versao, concessao), ct);
        if (obtida == 0) return;
        db.ChangeTracker.Clear();
        try
        {
            var op = await db.OperacoesPedido.Include(o => o.Pedido).ThenInclude(p => p.EnderecoEntrega)
                .Include(o => o.Pedido).ThenInclude(p => p.ItensPedido).Include(o => o.Movimentos).SingleAsync(o => o.Id == id, ct);
            var p = op.Pedido;
            if (p.OperacaoPendenteId != id) throw new ValidationException("A solicitação precisa de conferência pelo restaurante.");
            var principal = await db.PagamentosOnline.SingleOrDefaultAsync(x => x.PedidoId == p.Id, ct);
            bool pago = false, encerrado = false;
            if (principal != null)
            {
                if (principal.OrderId == null)
                {
                    // Recupera a mesma criação após timeout, sem inventar outra cobrança.
                    using var criada = await api.Enviar(null, principal.CorpoCriacao, principal.Referencia.ToString(), ct);
                    pagamentos.Conferir(criada.RootElement, principal);
                    var orderId = DadosOrder.Texto(criada.RootElement, "id");
                    if (!MpOrdersClient.IdValido(orderId)) throw new FalhaPagamento("Cobrança sem identificação válida.");
                    principal.OrderId = orderId; principal.Versao = Guid.NewGuid();
                    await db.SaveChangesAsync(ct);
                }
                using var consulta = await api.Enviar(principal.OrderId, null, null, ct);
                pagamentos.Conferir(consulta.RootElement, principal);
                pago = await CreditoConhecido(consulta.RootElement, principal.Valor, ct);
                // Um plano de devolução persistido deve ser retomado com as mesmas chaves após timeout.
                // A consistência externa é conferida novamente antes de aplicar o endereço.
                if (op.PagamentoConfirmado && op.Movimentos.Any(m => m.Tipo == TipoMovimentoPedido.Devolucao)) pago = true;
                encerrado = DadosOrder.EncerradaSemPagamento(consulta.RootElement);
                if (!pago && !encerrado)
                {
                    using var cancelada = await api.Operar(principal.OrderId!, "cancel", null, op.Id.ToString("N") + "-principal", ct);
                    using var conferir = await api.Enviar(principal.OrderId, null, null, ct);
                    pagamentos.Conferir(conferir.RootElement, principal);
                    pago = await CreditoConhecido(conferir.RootElement, principal.Valor, ct);
                    encerrado = DadosOrder.EncerradaSemPagamento(conferir.RootElement);
                    if (!pago && !encerrado) throw new FalhaPagamento("O pagamento ainda está sendo processado. Aguarde a conferência.");
                }
            }
            else if (op.Tipo == TipoOperacaoPedido.Cancelamento && op.AdministradorSolicitanteId.HasValue)
            {
                // Pagamentos antigos/manuais não têm uma order online; não inventa confirmação do provedor.
                pago = p.StatusPagamento == StatusPagamento.Aprovado;
            }
            else if (p.StatusPagamento is StatusPagamento.Aprovado or StatusPagamento.Conferencia or StatusPagamento.Reembolsado)
                throw new ValidationException("O pagamento deste pedido precisa ser conferido pelo restaurante antes da alteração.");
            op.PagamentoConfirmado = pago;

            if (op.Tipo == TipoOperacaoPedido.Cancelamento)
            {
                if (op.AdministradorSolicitanteId.HasValue
                    ? p.StatusPedido is not (StatusPedido.Recebido or StatusPedido.EmPreparo)
                    : p.StatusPedido != StatusPedido.Recebido) throw new ValidationException("O preparo já começou. O restaurante precisa conferir a solicitação.");
                p.StatusPedido = StatusPedido.Cancelado; p.CanceladoEmUtc = Agora; p.MotivoCancelamento = op.Motivo;
                p.ReembolsoCancelamentoPendente = pago || p.StatusPagamento == StatusPagamento.Conferencia;
                p.StatusPagamento = p.StatusPagamento is StatusPagamento.Conferencia or StatusPagamento.Reembolsado
                    ? p.StatusPagamento : pago ? StatusPagamento.Aprovado : StatusPagamento.Cancelado;
                if (principal != null) principal.RevisaoNecessaria = pago;
                op.Mensagem = pago ? "Pedido cancelado. O restaurante precisa conferir a devolução do pagamento. Use Preciso de ajuda para acompanhar."
                    : "Pedido cancelado antes do preparo. Nenhuma cobrança confirmada foi identificada.";
                if (op.AdministradorSolicitanteId.HasValue)
                {
                    await AvisarCozinhaDoCancelamento(op, ct);
                    op.Mensagem = p.ReembolsoCancelamentoPendente
                        ? "Pedido cancelado pelo restaurante. A devolução do valor precisa ser conferida; o cancelamento não confirma estorno."
                        : p.StatusPagamento == StatusPagamento.Reembolsado
                            ? "Pedido cancelado pelo restaurante. O pagamento consta como reembolsado no histórico."
                            : "Pedido cancelado pelo restaurante. Nenhuma cobrança confirmada foi identificada no sistema.";
                }
                Concluir(op); await db.SaveChangesAsync(ct); return;
            }

            var foraDaEtapa = !PodeAlterarEndereco(p);
            if (principal != null && encerrado)
            {
                op.PagamentoAnteriorJson = JsonSerializer.Serialize(new { principal.Referencia, principal.OrderId, principal.Valor,
                    principal.CorpoCriacao, principal.CriadoEmUtc, principal.SituacaoExterna, principal.RevisaoFinanceira });
                db.PagamentosOnline.Remove(principal); p.StatusPagamento = StatusPagamento.Pendente;
            }
            var desistir = op.DesistenciaSolicitada || foraDaEtapa || Agora > op.CriadaEmUtc.AddMinutes(30);
            if (pago && op.Diferenca > 0)
            {
                var cob = op.Movimentos.SingleOrDefault(m => m.Tipo == TipoMovimentoPedido.Cobranca);
                if (cob == null && !desistir)
                {
                    cob = new() { OperacaoPedidoId = op.Id, Tipo = TipoMovimentoPedido.Cobranca, Valor = op.Diferenca, CriadoEmUtc = Agora };
                    cob.Corpo = await CorpoCobranca(p, cob.Id, cob.Valor, ct);
                    op.Movimentos.Add(cob); db.MovimentosPedido.Add(cob); await db.SaveChangesAsync(ct);
                }
                if (cob != null)
                {
                    if (cob.OrderId == null)
                    {
                        using var criada = await api.Enviar(null, cob.Corpo, cob.Id.ToString("N"), ct);
                        ConferirCobranca(criada.RootElement, cob);
                        cob.OrderId = DadosOrder.Texto(criada.RootElement, "id"); cob.CheckoutUrl = DadosOrder.Texto(criada.RootElement, "checkout_url");
                        if (!MpOrdersClient.IdValido(cob.OrderId) || !MpOrdersClient.UrlValida(cob.CheckoutUrl)) throw new FalhaPagamento("Link de ajuste inválido.");
                        cob.Versao = Guid.NewGuid(); await db.SaveChangesAsync(ct);
                    }
                    using var consulta = await api.Enviar(cob.OrderId, null, null, ct);
                    ConferirCobranca(consulta.RootElement, cob);
                    if (await CreditoConhecido(consulta.RootElement, cob.Valor, ct))
                    { cob.Estado = EstadoMovimentoPedido.Confirmado; cob.ConfirmadoEmUtc ??= Agora; cob.Versao = Guid.NewGuid(); await db.SaveChangesAsync(ct); }
                    else if (desistir || DadosOrder.EncerradaSemPagamento(consulta.RootElement))
                    {
                        if (!DadosOrder.EncerradaSemPagamento(consulta.RootElement))
                        {
                            using var cancelar = await api.Operar(cob.OrderId!, "cancel", null, cob.Id.ToString("N") + "-cancelar", ct);
                            using var confirmacao = await api.Enviar(cob.OrderId, null, null, ct);
                            ConferirCobranca(confirmacao.RootElement, cob);
                            if (!DadosOrder.EncerradaSemPagamento(confirmacao.RootElement)) throw new FalhaPagamento("A cobrança adicional precisa de conferência.");
                        }
                        cob.Estado = EstadoMovimentoPedido.Encerrado;
                        Desfazer(op, "A alteração foi encerrada sem aplicar o novo endereço. O endereço anterior continua válido.");
                        await db.SaveChangesAsync(ct); return;
                    }
                    else
                    {
                        op.Estado = EstadoOperacaoPedido.AguardandoPagamento;
                        op.Mensagem = "Pague somente a diferença de entrega. O novo endereço valerá após a confirmação, desde que o pedido ainda não tenha saído para entrega.";
                        await db.SaveChangesAsync(ct); return;
                    }
                }
            }

            if (desistir && !op.Movimentos.Any(m => m.Tipo == TipoMovimentoPedido.Devolucao))
            {
                if (op.Movimentos.Any(m => m.Estado == EstadoMovimentoPedido.Confirmado || m.Tipo == TipoMovimentoPedido.Devolucao))
                    throw new ValidationException("Houve movimentação financeira, mas a alteração não pode mais ser aplicada. Peça ajuda ao restaurante para conferir os valores.");
                Desfazer(op, "Alteração encerrada. O endereço anterior foi mantido."); await db.SaveChangesAsync(ct); return;
            }

            if (pago && op.Diferenca < 0)
            {
                await PrepararDevolucoes(op, principal!, ct);
                foreach (var mov in op.Movimentos.Where(m => m.Tipo == TipoMovimentoPedido.Devolucao && m.Estado != EstadoMovimentoPedido.Confirmado))
                {
                    using var resposta = await api.Operar(mov.OrderId!, "refund", mov.Corpo, mov.Id.ToString("N"), ct);
                    if (DadosOrder.Texto(resposta.RootElement, "id") != mov.OrderId) throw new FalhaPagamento("A devolução recebida não corresponde à cobrança.");
                    var anteriores = JsonSerializer.Deserialize<string[]>(mov.ReembolsosAnterioresJson) ?? [];
                    var reembolso = DadosOrder.Transacoes(resposta.RootElement, "refunds").SingleOrDefault(r =>
                        DadosOrder.Texto(r, "transaction_id") == mov.TransacaoId && DadosOrder.Valor(r, "amount") == mov.Valor
                        && (mov.ReembolsoId != null ? DadosOrder.Texto(r, "id") == mov.ReembolsoId : !anteriores.Contains(DadosOrder.Texto(r, "id"))));
                    var refId = DadosOrder.Texto(reembolso, "id");
                    if (!MpOrdersClient.IdValido(refId)) throw new FalhaPagamento("A devolução está em conferência.");
                    if (mov.ReembolsoId != null && mov.ReembolsoId != refId) throw new FalhaPagamento("A identificação da devolução mudou.");
                    mov.ReembolsoId = refId;
                    if (DadosOrder.Texto(reembolso, "status") != "processed")
                    {
                        op.Estado = EstadoOperacaoPedido.AguardandoDevolucao; op.Mensagem = "A devolução da diferença foi solicitada e aguarda confirmação do Mercado Pago.";
                        await db.SaveChangesAsync(ct); return;
                    }
                    mov.Estado = EstadoMovimentoPedido.Confirmado; mov.ConfirmadoEmUtc = Agora; mov.Versao = Guid.NewGuid();
                    await db.SaveChangesAsync(ct);
                }
            }

            if (desistir) throw new ValidationException("A etapa do pedido mudou durante o ajuste. Peça ajuda ao restaurante para conferir os valores.");
            if (pago) await ConferirSaldoFinal(op, principal!, ct);

            // Reconfere a etapa e a versão no mesmo SaveChanges que efetiva o endereço e o total.
            var novo = JsonSerializer.Deserialize<EnderecoEntrega>(op.NovoEnderecoJson) ?? throw new ValidationException("Endereço inválido.");
            Preencher(p.EnderecoEntrega, novo);
            p.TaxaEntrega = op.NovaTaxa; p.DistanciaEntregaMetros = op.DistanciaMetros;
            p.DuracaoPercursoSegundos = op.DuracaoSegundos; p.PreparoMinutos ??= 30;
            p.ValorTotal = p.ItensPedido.Sum(i => i.Subtotal) - p.DescontoCupom + p.TaxaEntrega;
            p.RevisaoFinanceira++; op.RevisaoFinanceiraAplicada = p.RevisaoFinanceira;
            if (pago) p.StatusPagamento = StatusPagamento.Aprovado;
            if (op.EnderecoSalvarJson != null)
            {
                var salvar = JsonSerializer.Deserialize<EnderecoUsuario>(op.EnderecoSalvarJson)!;
                salvar.Id = 0; salvar.UsuarioId = p.UsuarioId; salvar.Versao = Guid.NewGuid(); db.EnderecosUsuario.Add(salvar);
            }
            op.Mensagem = pago && op.Diferenca < 0 ? "Endereço atualizado. A diferença foi devolvida pelo provedor; o prazo de crédito depende do meio de pagamento."
                : "Endereço e total do pedido atualizados. Confira os novos dados de entrega.";
            Concluir(op); await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is FalhaPagamento or ValidationException or DbUpdateException or JsonException or InvalidOperationException or FormatException)
        {
            logger.LogWarning("Operação do pedido {Operacao}: {Tipo}, {Origem}.", id, ex.GetType().Name, ex.StackTrace?.Split('\n').FirstOrDefault());
            db.ChangeTracker.Clear();
            var tipo = await db.OperacoesPedido.Where(o => o.Id == id).Select(o => o.Tipo).SingleAsync(ct);
            var mensagem = tipo == TipoOperacaoPedido.Cancelamento
                ? "Não foi possível concluir o cancelamento agora. Confira a situação do pedido, atualize o andamento ou use Preciso de ajuda."
                : "Não foi possível concluir a alteração agora. O endereço atual foi mantido. Atualize o andamento ou use Preciso de ajuda; não repita o pagamento por fora.";
            await db.OperacoesPedido.Where(o => o.Id == id && o.Estado != EstadoOperacaoPedido.Concluida && o.Estado != EstadoOperacaoPedido.Desfeita)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Estado, EstadoOperacaoPedido.Conferencia)
                    .SetProperty(o => o.Mensagem, mensagem), ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.OperacoesPedido.Where(o => o.Id == id && o.Versao == concessao).ExecuteUpdateAsync(s => s.SetProperty(o => o.ProcessandoAteUtc, (DateTime?)null)
                .SetProperty(o => o.ConferidaEmUtc, Agora), CancellationToken.None);
        }
    }

    private void Concluir(OperacaoPedido op)
    {
        op.Estado = EstadoOperacaoPedido.Concluida; op.ConcluidaEmUtc = Agora; op.Versao = Guid.NewGuid();
        op.Pedido.OperacaoPendenteId = null; op.Pedido.Versao = Guid.NewGuid();
    }
    private void Desfazer(OperacaoPedido op, string mensagem)
    { Concluir(op); op.Estado = EstadoOperacaoPedido.Desfeita; op.Mensagem = mensagem; }

    private async Task<bool> CreditoConhecido(JsonElement json, decimal valor, CancellationToken ct)
    {
        if (DadosOrder.Aprovada(json, valor) && DadosOrder.Transacoes(json, "refunds").Length == 0) return true;
        if (!((DadosOrder.Texto(json, "status") == "processed" && DadosOrder.Texto(json, "status_detail") == "partially_refunded")
            || DadosOrder.Texto(json, "status") == "refunded")) return false;
        var id = DadosOrder.Texto(json, "id");
        var locais = await db.MovimentosPedido.AsNoTracking().Where(m => m.OrderId == id && m.Tipo == TipoMovimentoPedido.Devolucao
            && m.Estado == EstadoMovimentoPedido.Confirmado).ToListAsync(ct);
        var externos = DadosOrder.Transacoes(json, "refunds");
        var pago = DadosOrder.Valor(json, "total_paid_amount");
        return locais.Count > 0 && externos.Length == locais.Count && (pago == valor || pago == valor - locais.Sum(m => m.Valor))
            && (DadosOrder.Texto(json, "status") != "refunded" || locais.Sum(m => m.Valor) == valor)
            && externos.All(r => DadosOrder.Texto(r, "status") == "processed" && locais.Any(m => m.ReembolsoId == DadosOrder.Texto(r, "id")
                && m.TransacaoId == DadosOrder.Texto(r, "transaction_id") && m.Valor == DadosOrder.Valor(r, "amount")));
    }

    private async Task ConferirSaldoFinal(OperacaoPedido op, PagamentoOnline principal, CancellationToken ct)
    {
        var cobrancas = await db.MovimentosPedido.AsNoTracking().Where(m => m.Operacao.PedidoId == op.PedidoId
            && m.Tipo == TipoMovimentoPedido.Cobranca && m.Estado == EstadoMovimentoPedido.Confirmado)
            .Select(m => new { m.OrderId, Referencia = m.Id, m.Valor }).ToListAsync(ct);
        var fontes = cobrancas.Select(m => (m.OrderId!, m.Referencia, m.Valor)).ToList();
        fontes.Add((principal.OrderId!, principal.Referencia, principal.Valor));
        decimal saldo = 0;
        foreach (var (orderId, referencia, valor) in fontes)
        {
            using var consulta = await api.Enviar(orderId, null, null, ct);
            pagamentos.Conferir(consulta.RootElement, new PagamentoOnline { Referencia = referencia, Valor = valor });
            if (!await CreditoConhecido(consulta.RootElement, valor, ct)) throw new FalhaPagamento("O saldo do pagamento precisa de conferência.");
            var devolucoes = await db.MovimentosPedido.AsNoTracking().Where(m => m.OrderId == orderId
                && m.Tipo == TipoMovimentoPedido.Devolucao && m.Estado == EstadoMovimentoPedido.Confirmado).Select(m => m.Valor).ToListAsync(ct);
            saldo += valor - devolucoes.Sum();
        }
        var esperado = op.Pedido.ItensPedido.Sum(i => i.Subtotal) - op.Pedido.DescontoCupom + op.NovaTaxa;
        if (saldo != esperado) throw new FalhaPagamento("O saldo confirmado não corresponde ao novo total.");
    }

    private void ConferirCobranca(JsonElement json, MovimentoPedido m) => pagamentos.Conferir(json,
        new PagamentoOnline { Referencia = m.Id, Valor = m.Valor });

    private async Task<string> CorpoCobranca(Pedido p, Guid referencia, decimal valor, CancellationToken ct)
    {
        var o = config.Value;
        var email = o.ModoTeste ? o.EmailCompradorTeste : await db.Usuarios.Where(u => u.Id == p.UsuarioId).Select(u => u.Email).SingleAsync(ct);
        if (!new EmailAddressAttribute().IsValid(email) || !Uri.TryCreate(o.UrlPublica, UriKind.Absolute, out var url) || url.Scheme != "https")
            throw new FalhaPagamento("Confira a configuração do pagamento.");
        var retorno = o.UrlPublica.TrimEnd('/') + "/Pedido/Detalhes/" + p.Id;
        return JsonSerializer.Serialize(new { type = "online", processing_mode = "manual", capture_mode = "automatic_async",
            total_amount = Dinheiro(valor), external_reference = referencia.ToString("N"), expiration_time = "PT30M", payer = new { email },
            items = new[] { new { title = "Diferença de entrega — pedido #" + p.Id, quantity = 1, unit_price = Dinheiro(valor) } },
            config = new { online = new { success_url = retorno, failure_url = retorno, pending_url = retorno, auto_return = "all" } } });
    }

    private async Task PrepararDevolucoes(OperacaoPedido op, PagamentoOnline principal, CancellationToken ct)
    {
        if (op.Movimentos.Any(m => m.Tipo == TipoMovimentoPedido.Devolucao)) return;
        var anteriores = await db.MovimentosPedido.AsNoTracking().Where(m => m.Operacao.PedidoId == op.PedidoId
            && m.Tipo == TipoMovimentoPedido.Cobranca && m.Estado == EstadoMovimentoPedido.Confirmado
            && m.Operacao.Estado == EstadoOperacaoPedido.Concluida).OrderByDescending(m => m.CriadoEmUtc).ToListAsync(ct);
        var fontes = anteriores.Select(m => (m.OrderId!, m.Id, m.Valor)).ToList();
        fontes.Add((principal.OrderId!, principal.Referencia, principal.Valor));
        var falta = -op.Diferenca;
        foreach (var (orderId, referencia, valor) in fontes)
        {
            using var consulta = await api.Enviar(orderId, null, null, ct);
            pagamentos.Conferir(consulta.RootElement, new PagamentoOnline { Referencia = referencia, Valor = valor });
            if (!await CreditoConhecido(consulta.RootElement, valor, ct)) continue;
            var jaDevolvidos = await db.MovimentosPedido.AsNoTracking().Where(m => m.OrderId == orderId
                && m.Tipo == TipoMovimentoPedido.Devolucao && m.Estado == EstadoMovimentoPedido.Confirmado).ToListAsync(ct);
            decimal alocadoFonte = 0;
            foreach (var pagamento in DadosOrder.Transacoes(consulta.RootElement, "payments"))
            {
                var transacao = DadosOrder.Texto(pagamento, "id"); var totalTransacao = DadosOrder.Valor(pagamento, "amount");
                if (!MpOrdersClient.IdValido(transacao) || !totalTransacao.HasValue) continue;
                var disponivel = totalTransacao.Value - jaDevolvidos.Where(m => m.TransacaoId == transacao).Sum(m => m.Valor);
                var parcela = Math.Min(falta, Math.Min(disponivel, valor - jaDevolvidos.Sum(m => m.Valor) - alocadoFonte));
                if (parcela <= 0) continue;
                var movimento = new MovimentoPedido { OperacaoPedidoId = op.Id, Tipo = TipoMovimentoPedido.Devolucao,
                    OrderId = orderId, TransacaoId = transacao, Valor = parcela, CriadoEmUtc = Agora,
                    ReembolsosAnterioresJson = JsonSerializer.Serialize(jaDevolvidos.Select(m => m.ReembolsoId).ToArray()),
                    Corpo = JsonSerializer.Serialize(new { transactions = new[] { new { id = transacao, amount = Dinheiro(parcela) } } }) };
                op.Movimentos.Add(movimento); db.MovimentosPedido.Add(movimento);
                falta -= parcela;
                alocadoFonte += parcela;
                if (falta == 0) break;
            }
            if (falta == 0) break;
        }
        if (falta != 0) throw new FalhaPagamento("Não foi possível confirmar saldo para devolver a diferença.");
        // O plano completo é persistido antes da primeira chamada de devolução.
        await db.SaveChangesAsync(ct);
    }

    private static EnderecoEntrega Copiar(EnderecoEntrega e) => new() { Cep = e.Cep, Logradouro = e.Logradouro, Numero = e.Numero,
        Bairro = e.Bairro, Cidade = e.Cidade, Uf = e.Uf, Complemento = e.Complemento, Referencia = e.Referencia };
    private static void Preencher(EnderecoEntrega alvo, EnderecoEntrega e)
    { alvo.Cep = e.Cep; alvo.Logradouro = e.Logradouro; alvo.Numero = e.Numero; alvo.Bairro = e.Bairro; alvo.Cidade = e.Cidade;
      alvo.Uf = e.Uf; alvo.Complemento = e.Complemento; alvo.Referencia = e.Referencia; }
}
