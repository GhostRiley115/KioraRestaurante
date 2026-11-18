using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class RegrasAcomodacaoTests
{
    [Theory]
    [InlineData(SituacaoReserva.Aguardando)]
    [InlineData(SituacaoReserva.Chamada)]
    public async Task EsperaVencidaNaoAbreComandaMesmoAntesDaManutencao(SituacaoReserva situacao)
    {
        using var b = new BancoSalao();
        var agora = b.Relogio.GetUtcNow().UtcDateTime;
        var reserva = new Reserva
        {
            Nome = "Grupo aguardando", Telefone = "11900001234", Pessoas = 2,
            Tipo = TipoReserva.Fila, Situacao = situacao, MesaId = situacao == SituacaoReserva.Chamada ? 1 : null,
            CriadaEmUtc = agora.AddMinutes(-30), ChegadaEmUtc = agora.AddMinutes(-30),
            ExpiraEmUtc = agora.AddSeconds(-1), ChaveSolicitacao = Guid.NewGuid()
        };
        b.Db.Reservas.Add(reserva);
        await b.Db.SaveChangesAsync();
        var mesa = await b.Db.MesasRestaurante.AsNoTracking().SingleAsync(m => m.Id == 1);

        await Assert.ThrowsAsync<ValidationException>(() => b.Comandas.Acomodar(b.Garcom.Id, new()
        {
            MesaId = mesa.Id, Versao = mesa.Versao, ReservaCodigo = reserva.Codigo,
            ChaveSolicitacao = Guid.NewGuid()
        }));

        Assert.Empty(await b.Db.AtendimentosMesa.ToListAsync());
        Assert.Empty(await b.Db.MovimentosSalao.ToListAsync());
        Assert.Equal(situacao, (await b.Db.Reservas.AsNoTracking().SingleAsync()).Situacao);
    }

    [Fact]
    public async Task EscolhaDeResponsavelExcluiConvitePendenteEContaDesativada()
    {
        using var b = new BancoSalao();
        await b.Db.Usuarios.Where(u => u.Id == b.OutroGarcom.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ConvitePendente, true));
        await b.Db.Usuarios.Where(u => u.Id == b.Admin.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Ativo, false));

        var opcoes = await b.Consulta.Garcons(b.Gerente.Id);

        Assert.Contains(opcoes, u => u.Id == b.Garcom.Id);
        Assert.Contains(opcoes, u => u.Id == b.Gerente.Id);
        Assert.DoesNotContain(opcoes, u => u.Id == b.OutroGarcom.Id);
        Assert.DoesNotContain(opcoes, u => u.Id == b.Admin.Id);
    }
}
