using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Banco=KioraRestaurante.Tests.CheckoutEntregaTests.Banco;
namespace KioraRestaurante.Tests;

public class CuponsReutilizaveisTests
{
    private static async Task<int> Criar(Banco b,int? porCliente,int? total=null)
    {
        var admin=await CuponsTests.Administrador(b);var agora=HorarioRestaurante.ParaLocal(b.Relogio.GetUtcNow().UtcDateTime);
        return await b.Cupons.Salvar(admin.Id,new(){Codigo="PARCEIRO10",Descricao="Campanha de teste",Valor=10,InicioLocal=agora.AddHours(-1),FimLocal=agora.AddDays(1),LimitePorCliente=porCliente,LimiteTotal=total});
    }
    private static async Task Reservar(Banco b,KioraRestaurante.DTOs.Cupons.CupomAplicado cupom)
    {
        await using var tx=await b.Db.Database.BeginTransactionAsync();var pedido=new Pedido{UsuarioId=b.Cliente.Id,ValorTotal=36,DataPedido=b.Relogio.Agora.UtcDateTime,CodigoCupom=cupom.Codigo,DescontoCupom=cupom.Desconto};
        await b.Cupons.Reservar(pedido,cupom);b.Db.Pedidos.Add(pedido);await b.Db.SaveChangesAsync();await tx.CommitAsync();
    }
    [Fact] public async Task ReutilizavelPermiteNovosPedidosDoMesmoClienteSemAlterarValidade()
    {
        using var b=new Banco();await Criar(b,null);var resumo=(await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        for(var i=0;i<4;i++)await Reservar(b,await b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));
        Assert.Equal(4,await b.Db.UsosCupom.CountAsync());Assert.Null((await b.Db.Cupons.SingleAsync()).LimitePorCliente);
        b.Relogio.Agora=b.Relogio.Agora.AddDays(2);await Assert.ThrowsAsync<ValidationException>(()=>b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));
    }
    [Fact] public async Task UsoUnicoBloqueiaOutraCompraMasCancelamentoLiberaUso()
    {
        using var b=new Banco();await Criar(b,1);var resumo=(await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        await Reservar(b,await b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));await Assert.ThrowsAsync<ValidationException>(()=>b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));
        var p=await b.Db.Pedidos.SingleAsync();p.StatusPedido=StatusPedido.Cancelado;await b.Db.SaveChangesAsync();
        await Reservar(b,await b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));Assert.Equal(1,(await b.Db.Cupons.SingleAsync()).UtilizacoesAtivas);Assert.Equal(2,await b.Db.UsosCupom.CountAsync());
    }
    [Fact] public async Task SemLimiteIndividualContinuaBloqueadoPelaCotaTotalInclusiveCotacaoAntiga()
    {
        using var b=new Banco();await Criar(b,null,1);var resumo=(await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;var antigo=await b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo);
        await Reservar(b,antigo);await Assert.ThrowsAsync<ValidationException>(()=>Reservar(b,antigo));b.Db.ChangeTracker.Clear();
        Assert.Single(await b.Db.UsosCupom.ToListAsync());Assert.Equal(1,(await b.Db.Cupons.SingleAsync()).UtilizacoesAtivas);
        await Assert.ThrowsAsync<ValidationException>(()=>b.Cupons.Cotar(b.Cliente.Id,"parceiro10",resumo));
    }
    [Fact] public async Task EdicaoPreservaLimitesAntigosEPermiteTrocarParaReutilizavel()
    {
        using var b=new Banco();var id=await Criar(b,3);var admin=await b.Db.Usuarios.SingleAsync(u=>u.Tipo==TipoUsuario.Administrador);
        var dto=await b.Cupons.BuscarEdicao(admin.Id,id);Assert.Equal(3,dto.LimitePorCliente);dto.LimitePorCliente=null;
        await b.Cupons.Salvar(admin.Id,dto);Assert.Null((await b.Cupons.BuscarEdicao(admin.Id,id)).LimitePorCliente);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1000001)]
    public async Task LimitesIndividuaisInvalidosContinuamRejeitados(int limite)
    {using var b=new Banco();await Assert.ThrowsAsync<ValidationException>(()=>Criar(b,limite));Assert.Empty(await b.Db.Cupons.ToListAsync());}
}
