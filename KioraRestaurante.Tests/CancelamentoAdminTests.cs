using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KioraRestaurante.Tests;

public class CancelamentoAdminTests
{
    private static async Task<int> Conta(GerenciarPedidoTests.Banco b, TipoUsuario tipo=TipoUsuario.Administrador, bool ativo=true)
    {
        var u=new Usuario{Nome="Equipe de teste",Email=Guid.NewGuid()+"@example.com",SenhaHash="sem uso",Tipo=tipo,Ativo=ativo};
        b.Db.Usuarios.Add(u);await b.Db.SaveChangesAsync();b.Db.ChangeTracker.Clear();return u.Id;
    }
    private static async Task<CancelarPedidoAdminRequestDTO> Dados(GerenciarPedidoTests.Banco b) => new(){PedidoId=b.Id,VersaoPedido=(await b.Pedido()).Versao,Motivo="  Ingrediente indisponível  "};

    [Theory][InlineData(StatusPedido.Recebido)][InlineData(StatusPedido.EmPreparo)]
    public async Task CancelaEtapasPermitidasERegistraResponsavelSemAlterarValores(StatusPedido etapa)
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);
        await b.Db.Pedidos.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.StatusPedido,etapa));
        var antes=await b.Pedido();var dto=await Dados(b);
        var resultado=await b.Service.CancelarPeloAdministrador(admin,dto);
        Assert.True(resultado.Concluido);Assert.False(resultado.ReembolsoPendente);
        var p=await b.Pedido();Assert.Equal(StatusPedido.Cancelado,p.StatusPedido);Assert.Equal(antes.ValorTotal,p.ValorTotal);Assert.Equal(antes.TaxaEntrega,p.TaxaEntrega);
        Assert.Equal("Ingrediente indisponível",p.MotivoCancelamento);Assert.NotNull(p.CanceladoEmUtc);Assert.Null(p.OperacaoPendenteId);Assert.NotEqual(antes.Versao,p.Versao);
        var op=await b.Db.OperacoesPedido.SingleAsync();Assert.Equal(admin,op.AdministradorSolicitanteId);Assert.Equal("Equipe de teste",op.AdministradorSolicitanteNome);Assert.Equal(EstadoOperacaoPedido.Concluida,op.Estado);
        Assert.True((await b.Service.CancelarPeloAdministrador(admin,dto)).Concluido);Assert.Single(await b.Db.OperacoesPedido.ToListAsync());Assert.Single(await b.Db.Notificacoes.ToListAsync());
        var detalhe=await new AdminService(b.Db).DetalhesPedido(admin,b.Id);Assert.NotNull(detalhe);Assert.Equal(admin,Assert.Single(detalhe.Operacoes).AdministradorId);
    }
    [Theory][InlineData(StatusPedido.SaiuParaEntrega)][InlineData(StatusPedido.Entregue)]
    public async Task NaoCancelaAposSaida(StatusPedido etapa)
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);await b.Db.Pedidos.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.StatusPedido,etapa));
        var dto=await Dados(b);await Assert.ThrowsAsync<ValidationException>(()=>b.Service.CancelarPeloAdministrador(admin,dto));
        Assert.Equal(etapa,(await b.Pedido()).StatusPedido);Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }
    [Theory][InlineData(TipoUsuario.Cliente)][InlineData(TipoUsuario.Gerente)][InlineData(TipoUsuario.Garcom)][InlineData(TipoUsuario.Portaria)][InlineData(TipoUsuario.Cozinha)]
    public async Task OutrosCargosNaoGanhamPermissaoAdministrativa(TipoUsuario tipo)
    {
        using var b=new GerenciarPedidoTests.Banco(false);var id=await Conta(b,tipo);var dto=await Dados(b);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Service.CancelarPeloAdministrador(id,dto));Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }
    [Fact] public async Task AdministradorInativoNaoCancelaMasClienteInativoPodeTerPedidoCanceladoPorAdminAtivo()
    {
        using var b=new GerenciarPedidoTests.Banco(false);var inativo=await Conta(b,ativo:false);var dto=await Dados(b);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Service.CancelarPeloAdministrador(inativo,dto));
        var admin=await Conta(b);await b.Db.Usuarios.Where(u=>u.Id==b.UsuarioId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Ativo,false));
        Assert.True((await b.Service.CancelarPeloAdministrador(admin,dto)).Concluido);
    }
    [Theory][InlineData("")][InlineData("   ")][InlineData("longo")]
    public async Task MotivoObrigatorioELimitadoAntesDeReservarOperacao(string motivo)
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);var dto=await Dados(b);dto.Motivo=motivo=="longo"?new string('a',301):motivo;
        await Assert.ThrowsAsync<ValidationException>(()=>b.Service.CancelarPeloAdministrador(admin,dto));Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());Assert.Null((await b.Pedido()).OperacaoPendenteId);
    }
    [Fact] public async Task VersaoAntigaEOperacaoPendenteImpedemCancelamento()
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);var dto=await Dados(b);dto.VersaoPedido=Guid.NewGuid();
        await Assert.ThrowsAsync<ValidationException>(()=>b.Service.CancelarPeloAdministrador(admin,dto));dto=await Dados(b);
        var operacao=Guid.NewGuid();await b.Db.Pedidos.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.OperacaoPendenteId,(Guid?)operacao));
        await Assert.ThrowsAsync<ValidationException>(()=>b.Service.CancelarPeloAdministrador(admin,dto));Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PagamentoOnlineEConferidoSemPrometerOuExecutarEstorno(bool pago)
    {
        using var b=new GerenciarPedidoTests.Banco(true,pago);var admin=await Conta(b);var resultado=await b.Service.CancelarPeloAdministrador(admin,await Dados(b));
        Assert.True(resultado.Concluido);Assert.Equal(pago,resultado.ReembolsoPendente);Assert.Equal(0,b.Provedor.Devolucoes);
        Assert.Equal(pago?0:1,b.Provedor.Cancelamentos);Assert.Equal(pago?StatusPagamento.Aprovado:StatusPagamento.Cancelado,(await b.Pedido()).StatusPagamento);
    }
    [Theory][InlineData(StatusPagamento.Pendente,false)][InlineData(StatusPagamento.Aprovado,true)][InlineData(StatusPagamento.Conferencia,true)][InlineData(StatusPagamento.Reembolsado,false)]
    public async Task PagamentoAntigoSemOrderNaoEInventadoNemApagado(StatusPagamento pagamento,bool revisao)
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);
        await b.Db.Pedidos.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.FormaPagamento,FormaPagamento.Pix).SetProperty(p=>p.StatusPagamento,pagamento));
        var resultado=await b.Service.CancelarPeloAdministrador(admin,await Dados(b));Assert.True(resultado.Concluido);Assert.Equal(revisao,resultado.ReembolsoPendente);
        Assert.Equal(pagamento==StatusPagamento.Pendente?StatusPagamento.Cancelado:pagamento,(await b.Pedido()).StatusPagamento);Assert.Equal(0,b.Provedor.Consultas);
    }
    [Fact] public async Task ClienteContinuaSemPoderCancelarDurantePreparo()
    {
        using var b=new GerenciarPedidoTests.Banco(false);await b.Db.Pedidos.ExecuteUpdateAsync(s=>s.SetProperty(p=>p.StatusPedido,StatusPedido.EmPreparo));
        var p=await b.Pedido();await Assert.ThrowsAsync<ValidationException>(()=>b.Service.Cancelar(b.UsuarioId,new(){PedidoId=p.Id,VersaoPedido=p.Versao,Motivo="Teste"},default));Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }
    [Fact] public async Task CozinhaRecebeAvisoComDestinoCorretoSemExporAOutrosClientes()
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);var cozinha=await Conta(b,TipoUsuario.Cozinha);var gerente=await Conta(b,TipoUsuario.Gerente);var cliente=await Conta(b,TipoUsuario.Cliente);var portaria=await Conta(b,TipoUsuario.Portaria);var inativo=await Conta(b,TipoUsuario.Cozinha,false);
        await b.Service.CancelarPeloAdministrador(admin,await Dados(b));var aviso=await b.Db.Notificacoes.SingleAsync();Assert.Equal(TipoNotificacao.CorrecaoCozinha,aviso.Tipo);Assert.Contains("Ingrediente indisponível",aviso.Mensagem);
        var ids=await b.Db.NotificacoesUsuarios.Select(n=>n.UsuarioId).OrderBy(i=>i).ToListAsync();Assert.Equal(new[]{admin,cozinha,gerente}.OrderBy(i=>i),ids);
        var service=new NotificacaoService(b.Db,new EphemeralDataProtectionProvider(),TimeProvider.System);var destino=await service.Abrir(cozinha,aviso.Id);Assert.Equal("Restaurante",destino!.Controller);Assert.Equal("Cozinha",destino.Action);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.Detalhe(cliente,aviso.Id));Assert.Empty(await service.Comunicados(admin));
    }
    [Fact] public async Task MudancaDuranteConsultaNaoCancelaPedidoQueJaSaiu()
    {
        using var b=new GerenciarPedidoTests.Banco(true,true);var admin=await Conta(b);b.Provedor.AoConsultar=()=>b.Db.Database.ExecuteSqlRaw("UPDATE Pedidos SET StatusPedido = 'SaiuParaEntrega';");
        var resultado=await b.Service.CancelarPeloAdministrador(admin,await Dados(b));Assert.False(resultado.Concluido);Assert.Equal(StatusPedido.SaiuParaEntrega,(await b.Pedido()).StatusPedido);Assert.Empty(await b.Db.Notificacoes.ToListAsync());Assert.Equal(EstadoOperacaoPedido.Conferencia,(await b.Db.OperacoesPedido.SingleAsync()).Estado);
    }
    [Fact] public async Task FalhaNaGravacaoNaoProduzAvisoFalsoERetentativaNaoDuplica()
    {
        using var b=new GerenciarPedidoTests.Banco(false);var admin=await Conta(b);
        await b.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_cancelamento BEFORE UPDATE OF StatusPedido ON Pedidos WHEN NEW.StatusPedido = 'Cancelado' BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;");
        var resultado=await b.Service.CancelarPeloAdministrador(admin,await Dados(b));Assert.False(resultado.Concluido);Assert.Equal(StatusPedido.Recebido,(await b.Pedido()).StatusPedido);Assert.Empty(await b.Db.Notificacoes.ToListAsync());
        await b.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER falhar_cancelamento;");var id=(await b.Db.OperacoesPedido.SingleAsync()).Id;await b.Service.Processar(id,default);await b.Service.Processar(id,default);
        Assert.Equal(StatusPedido.Cancelado,(await b.Pedido()).StatusPedido);Assert.Single(await b.Db.Notificacoes.ToListAsync());Assert.Single(await b.Db.OperacoesPedido.ToListAsync());
    }
}
