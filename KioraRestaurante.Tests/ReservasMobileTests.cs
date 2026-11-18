using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class ReservasMobileTests
{
    private static CriarReservaDTO Reserva(BancoSalao b)=>new(){Nome="Cliente visitante",Telefone="11999990000",Pessoas=2,
        Data=b.Horarios.Hoje.AddDays(1),Horario="12:00",ChaveSolicitacao=Guid.NewGuid()};

    [Fact]
    public async Task ReservaAnonimaPassaAoHistoricoDaContaSemExporOutrasReservas()
    {
        using var b=new BancoSalao();var hash=new string('A',64);var visitante=new AcessoReserva(null,hash);
        var propria=await b.Reservas.Criar(visitante,Reserva(b),false,default);
        var outra=Reserva(b);outra.Telefone="11999990001";
        var alheia=await b.Reservas.Criar(new(null,new string('B',64)),outra,false,default);
        Assert.Equal(1,await b.Reservas.VincularReservasVisitante(b.Cliente.Id,hash,default));
        var historico=await b.Reservas.Minhas(new(b.Cliente.Id,null),default);
        Assert.Equal(propria.Codigo,Assert.Single(historico).Codigo);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>b.Reservas.Consultar(visitante,propria.Codigo,default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>b.Reservas.Consultar(new(b.Cliente.Id,null),alheia.Codigo,default));
        var registro=await b.Db.Reservas.SingleAsync(r=>r.Codigo==propria.Codigo);
        Assert.Equal(b.Cliente.Id,registro.UsuarioId);Assert.Null(registro.VisitanteHash);
    }

    [Fact]
    public async Task ReservaCriadaConectadoVaiDiretoAoHistoricoSemCriarPedidoDelivery()
    {
        using var b=new BancoSalao();var acesso=new AcessoReserva(b.Cliente.Id,null);
        var reserva=await b.Reservas.Criar(acesso,Reserva(b),false,default);
        Assert.Equal(reserva.Codigo,Assert.Single(await b.Reservas.Minhas(acesso,default)).Codigo);
        Assert.Equal(SituacaoReserva.AguardandoChegada,reserva.Situacao);Assert.Null(reserva.Mesa);Assert.Null(reserva.FimUtc);
        Assert.Empty(await b.Db.Pedidos.ToListAsync());Assert.Empty(await b.Db.AtendimentosMesa.ToListAsync());
    }

    [Fact]
    public async Task VinculoRepetidoNaoDuplicaReservaEContaInativaNaoAssumeVisitante()
    {
        using var b=new BancoSalao();var hash=new string('A',64);
        await b.Reservas.Criar(new(null,hash),Reserva(b),false,default);
        await b.Db.Usuarios.Where(u=>u.Id==b.Cliente.Id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Ativo,false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Reservas.VincularReservasVisitante(b.Cliente.Id,hash,default));
        Assert.Null((await b.Db.Reservas.SingleAsync()).UsuarioId);
        await b.Db.Usuarios.Where(u=>u.Id==b.Cliente.Id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Ativo,true));
        Assert.Equal(1,await b.Reservas.VincularReservasVisitante(b.Cliente.Id,hash,default));
        Assert.Equal(0,await b.Reservas.VincularReservasVisitante(b.Cliente.Id,hash,default));
        Assert.Single(await b.Db.Reservas.ToListAsync());
    }
}
