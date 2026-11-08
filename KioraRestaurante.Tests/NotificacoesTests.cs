using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Notificacoes;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Services.Notificacoes;
using KioraRestaurante.Services.Pedidos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Banco = KioraRestaurante.Tests.CheckoutEntregaTests.Banco;

namespace KioraRestaurante.Tests;

public class NotificacoesTests
{
    private static NotificacaoService Service(Banco b) => new(b.Db, new EphemeralDataProtectionProvider(), b.Relogio);
    private static NotificacaoDispatcher Dispatcher(Banco b) => new(b.Db, b.Cupons, b.Relogio);

    [Fact] public async Task NovoPedidoAvisaSomenteDonoEAdministradoresSemDuplicarNaRetentativa()
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b);
        var outro = new Usuario { Nome = "Outro", Email = "outro@example.com", SenhaHash = "teste" }; b.Db.Add(outro); await b.Db.SaveChangesAsync();
        var id = await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false));
        Assert.Equal(2, await b.Db.EventosNotificacao.CountAsync()); Assert.Empty(await b.Db.Notificacoes.ToListAsync());
        var eventos = await b.Db.EventosNotificacao.Select(e => e.Id).ToListAsync();
        var dispatcher = Dispatcher(b); await dispatcher.Processar();
        foreach (var e in eventos) await dispatcher.Distribuir(e);
        Assert.Equal(2, await b.Db.Notificacoes.CountAsync());
        var service = Service(b);
        Assert.Equal(1, await service.NaoLidas(b.Cliente.Id)); Assert.Equal(1, await service.NaoLidas(admin.Id)); Assert.Equal(0, await service.NaoLidas(outro.Id));
        var avisoCliente = Assert.Single((await service.Listar(b.Cliente.Id, 1)).Itens);
        var destino = await service.Abrir(b.Cliente.Id, avisoCliente.Id); Assert.Equal("Pedido", destino!.Controller); Assert.Equal(id, destino.Id);
        Assert.Equal(0, await service.NaoLidas(b.Cliente.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarcarLida(outro.Id, avisoCliente.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Abrir(outro.Id, avisoCliente.Id));
        var avisoAdmin = Assert.Single((await service.Listar(admin.Id, 1)).Itens);
        Assert.Equal("Admin", (await service.Abrir(admin.Id, avisoAdmin.Id))!.Controller);
    }
    [Fact] public async Task MudancasReaisGeramAvisosMasSalvarSemMudarStatusNaoGera()
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b);
        var id = await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false));
        var andamento = new AndamentoPedidoService(b.Db);
        foreach (var status in new[] { StatusPedido.EmPreparo, StatusPedido.SaiuParaEntrega, StatusPedido.Entregue })
        {
            if(status==StatusPedido.SaiuParaEntrega){foreach(var item in await b.Db.ItensPedido.ToListAsync())item.SituacaoPreparo=KioraRestaurante.Models.Salao.SituacaoPreparo.Pronto;await b.Db.SaveChangesAsync();}
            var p = await b.Db.Pedidos.SingleAsync(); await andamento.Avancar(admin.Id, id, p.Versao, status);
        }
        var antes = await b.Db.EventosNotificacao.CountAsync(); await b.Db.SaveChangesAsync(); Assert.Equal(antes, await b.Db.EventosNotificacao.CountAsync());
        await Dispatcher(b).Processar();
        var tipos = await b.Db.NotificacoesUsuarios.Where(n => n.UsuarioId == b.Cliente.Id).Select(n => n.Notificacao.Tipo).ToListAsync();
        Assert.Contains(TipoNotificacao.PedidoEmPreparo, tipos); Assert.Contains(TipoNotificacao.PedidoSaiuParaEntrega, tipos); Assert.Contains(TipoNotificacao.PedidoEntregue, tipos);
        Assert.Equal(4, tipos.Count); Assert.Equal(StatusPagamento.Aprovado, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
    }
    [Fact] public async Task CancelamentoReconciliadoLiberaCupomEAvisaCliente()
    {
        using var b = new Banco(); await CuponsTests.Criar(b); var id = await b.Pedidos.ConfirmarPedido(b.Acesso, await CuponsTests.Aplicar(b));
        var p = await b.Db.Pedidos.SingleAsync(); p.StatusPedido = StatusPedido.Cancelado; await b.Db.SaveChangesAsync();
        await Dispatcher(b).Processar();
        Assert.Equal(0, (await b.Db.Cupons.SingleAsync()).UtilizacoesAtivas);
        Assert.Single(await b.Db.Notificacoes.Where(n => n.Tipo == TipoNotificacao.PedidoCancelado).ToListAsync());
    }
    [Fact] public async Task PagamentoAprovadoConfirmaCupomEAvisaSemNovaCobranca()
    {
        using var b = new Banco(); await CuponsTests.Criar(b); var dto = await CuponsTests.Aplicar(b); dto.FormaPagamento = FormaPagamento.MercadoPago;
        await b.Pedidos.ConfirmarPedido(b.Acesso, dto); var p = await b.Db.Pedidos.SingleAsync(); p.StatusPagamento = StatusPagamento.Aprovado; await b.Db.SaveChangesAsync();
        await Dispatcher(b).Processar();
        Assert.Equal(KioraRestaurante.Models.Cupons.SituacaoUsoCupom.Confirmado, (await b.Db.UsosCupom.SingleAsync()).Situacao);
        Assert.Single(await b.Db.Notificacoes.Where(n => n.Tipo == TipoNotificacao.PagamentoConfirmado).ToListAsync());
    }
    [Fact] public async Task ReservaDeVisitanteAvisaAdminEAtualizacaoDeReservaAvisaDono()
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b); var agora = b.Relogio.GetUtcNow().UtcDateTime;
        var visitante = new Reserva { Nome = "Visitante", Telefone = "11900000000", Pessoas = 2, CriadaEmUtc = agora, ExpiraEmUtc = agora.AddHours(1), ChaveSolicitacao = Guid.NewGuid() };
        var cliente = new Reserva { UsuarioId = b.Cliente.Id, Nome = "Cliente", Telefone = "11900000000", Pessoas = 1, CriadaEmUtc = agora, ExpiraEmUtc = agora.AddHours(1), ChaveSolicitacao = Guid.NewGuid() };
        b.Db.AddRange(visitante, cliente); await b.Db.SaveChangesAsync(); await Dispatcher(b).Processar();
        Assert.Equal(2, await Service(b).NaoLidas(admin.Id)); Assert.Equal(0, await Service(b).NaoLidas(b.Cliente.Id));
        cliente.Situacao = SituacaoReserva.Cancelada; await b.Db.SaveChangesAsync(); await Dispatcher(b).Processar();
        var s = Service(b); var aviso = Assert.Single((await s.Listar(b.Cliente.Id, 1)).Itens);
        var destino = await s.Abrir(b.Cliente.Id, aviso.Id); Assert.Equal("Reservas", destino!.Controller); Assert.Equal(cliente.Codigo, destino.Codigo);
    }
    [Fact] public async Task FalhaNaDistribuicaoNaoDeixaDestinatariosParciaisERetomadaConcluiUmaVez()
    {
        using var b = new Banco(); await CuponsTests.Administrador(b); await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false));
        await b.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_aviso BEFORE INSERT ON NotificacoesUsuarios BEGIN SELECT RAISE(ABORT, 'simulada'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Dispatcher(b).Distribuir(b.Db.EventosNotificacao.AsNoTracking().Select(e => e.Id).First()));
        Assert.Empty(await b.Db.Notificacoes.ToListAsync()); Assert.Equal(0, await b.Db.EventosNotificacao.CountAsync(e => e.ProcessadoEmUtc != null));
        await b.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER falhar_aviso;");
        await Dispatcher(b).Processar(); await Dispatcher(b).Processar(); Assert.Equal(2, await b.Db.Notificacoes.CountAsync());
    }
    [Theory] [InlineData(PublicoNotificacao.Cliente, 1)] [InlineData(PublicoNotificacao.Todos, 2)]
    public async Task ComunicadoTemPreviaPublicoLeituraEPublicacaoIdempotente(PublicoNotificacao publico, int quantidade)
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b);
        b.Db.Add(new Usuario { Nome = "Inativo", Email = "inativo@example.com", SenhaHash = "teste", Ativo = false }); await b.Db.SaveChangesAsync();
        var s = Service(b); var dto = new CriarNotificacaoDTO { Titulo = "Aviso", Mensagem = "Descontos da semana <script>teste</script>", Publico = publico };
        var previa = await s.Preparar(admin.Id, dto); Assert.Equal(quantidade, previa.Destinatarios);
        var id = await s.Publicar(admin.Id, dto); Assert.Equal(id, await s.Publicar(admin.Id, dto)); Assert.Single(await b.Db.Notificacoes.ToListAsync());
        Assert.Equal(quantidade, await b.Db.NotificacoesUsuarios.CountAsync()); await s.MarcarTodas(b.Cliente.Id); Assert.Equal(0, await s.NaoLidas(b.Cliente.Id));
        var relatorio = Assert.Single(await s.Comunicados(admin.Id)); Assert.Equal(1, relatorio.Leituras); Assert.Equal(quantidade, relatorio.Destinatarios);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => s.Preparar(b.Cliente.Id, dto));
    }
    [Theory] [InlineData("texto")] [InlineData("publico")] [InlineData("token")] [InlineData("expirado")]
    public async Task NaoPublicaPreviaAdulteradaOuExpirada(string mudanca)
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b); var s = Service(b);
        var dto = new CriarNotificacaoDTO { Titulo = "Aviso", Mensagem = "Olá" }; await s.Preparar(admin.Id, dto);
        if (mudanca == "texto") dto.Mensagem = "Outra";
        if (mudanca == "publico") dto.Publico = PublicoNotificacao.Todos;
        if (mudanca == "token") dto.TokenPrevia = "invalido";
        if (mudanca == "expirado") b.Relogio.Agora = b.Relogio.Agora.AddMinutes(11);
        await Assert.ThrowsAsync<ValidationException>(() => s.Publicar(admin.Id, dto)); Assert.Empty(await b.Db.Notificacoes.ToListAsync());
    }
    [Fact] public async Task DivulgacaoDeCupomExigeRegrasDaPreviaEMantemLinkInterno()
    {
        using var b = new Banco(); var cupomId = await CuponsTests.Criar(b); var admin = await b.Db.Usuarios.SingleAsync(u => u.Tipo == TipoUsuario.Administrador);
        var s = Service(b); var dto = new CriarNotificacaoDTO { Titulo = "Cupom", Mensagem = "Confira o desconto", CupomId = cupomId };
        await s.Preparar(admin.Id, dto); var c = await b.Db.Cupons.SingleAsync(); c.Versao = Guid.NewGuid(); await b.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => s.Publicar(admin.Id, dto)); await s.Preparar(admin.Id, dto);
        var id = await s.Publicar(admin.Id, dto); var destino = await s.Abrir(b.Cliente.Id, id); Assert.Equal("Cupons", destino!.Controller); Assert.Equal(cupomId, destino.Id);
    }
    [Fact] public async Task RebaixarAdminOuDesativarContaImpedeLerAvisosAdministrativos()
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b); await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false)); await Dispatcher(b).Processar();
        var s = Service(b); Assert.Equal(1, await s.NaoLidas(admin.Id)); admin.Tipo = TipoUsuario.Cliente; await b.Db.SaveChangesAsync(); Assert.Equal(0, await s.NaoLidas(admin.Id));
        admin.Ativo = false; await b.Db.SaveChangesAsync(); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => s.Listar(admin.Id, 1));
    }
    [Theory] [InlineData("salto")] [InlineData("cliente")] [InlineData("antiga")] [InlineData("pendencia")] [InlineData("pagamento")]
    public async Task AndamentoNaoAceitaMudancaInsegura(string caso)
    {
        using var b = new Banco(); var admin = await CuponsTests.Administrador(b); var id = await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false));
        var p = await b.Db.Pedidos.SingleAsync(); var versao = p.Versao;
        if (caso == "pendencia") p.OperacaoPendenteId = Guid.NewGuid();
        if (caso == "pagamento") p.FormaPagamento = FormaPagamento.MercadoPago;
        await b.Db.SaveChangesAsync();
        var service = new AndamentoPedidoService(b.Db);
        if (caso == "cliente") await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Avancar(b.Cliente.Id, id, versao, StatusPedido.EmPreparo));
        else await Assert.ThrowsAsync<ValidationException>(() => service.Avancar(admin.Id, id, caso == "antiga" ? Guid.NewGuid() : versao,
            caso == "salto" ? StatusPedido.Entregue : StatusPedido.EmPreparo));
        Assert.Equal(StatusPedido.Recebido, p.StatusPedido);
    }
}
