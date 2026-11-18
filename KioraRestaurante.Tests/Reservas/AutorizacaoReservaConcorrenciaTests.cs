using System.Data.Common;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KioraRestaurante.Tests;

public class AutorizacaoReservaConcorrenciaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContaDesativadaAntesDaTravaNaoCriaOuCancelaReserva(bool cancelar)
    {
        using var b = new BancoSalao();
        var acesso = new AcessoReserva(b.Cliente.Id, null);
        var dto = new CriarReservaDTO
        {
            Nome = "Grupo de teste", Telefone = "11900001234", Pessoas = 2,
            Data = b.Horarios.Hoje.AddDays(1), Horario = "12:00", ChaveSolicitacao = Guid.NewGuid()
        };
        var existente = cancelar ? await b.Reservas.Criar(acesso, dto, false, default) : null;
        await using var db = Contexto(b, () => b.Db.Usuarios.Where(u => u.Id == b.Cliente.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Ativo, false)));
        var service = new ReservaService(db, b.Horarios);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            if (existente == null) await service.Criar(acesso, dto, false, default);
            else await service.Cancelar(acesso, new() { Codigo = existente.Codigo, Versao = existente.Versao }, default);
        });

        var reservas = await b.Db.Reservas.AsNoTracking().ToListAsync();
        if (cancelar) Assert.Equal(SituacaoReserva.AguardandoChegada, Assert.Single(reservas).Situacao);
        else Assert.Empty(reservas);
    }

    [Fact]
    public async Task GestorQuePerdeuCargoAntesDaTravaNaoCancelaReserva()
    {
        using var b = new BancoSalao();
        var reserva = await b.Reservas.Criar(new(b.Cliente.Id, null), new()
        {
            Nome = "Grupo de teste", Telefone = "11900001234", Pessoas = 2,
            Data = b.Horarios.Hoje.AddDays(1), Horario = "12:00", ChaveSolicitacao = Guid.NewGuid()
        }, false, default);
        await using var db = Contexto(b, () => b.Db.Usuarios.Where(u => u.Id == b.Gerente.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tipo, TipoUsuario.Cliente)));
        var service = new ReservaService(db, b.Horarios);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Operar(b.Gerente.Id, new()
        {
            Codigo = reserva.Codigo, Versao = reserva.Versao, Acao = "cancelar", Motivo = "Correção de reserva"
        }, default));

        Assert.Equal(SituacaoReserva.AguardandoChegada, (await b.Db.Reservas.AsNoTracking().SingleAsync()).Situacao);
    }

    private static AppDbContext Contexto(BancoSalao b, Func<Task> revogar) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(b.Db.Database.GetDbConnection())
            .AddInterceptors(new RevogarAntesDaTrava(revogar)).Options);

    // Simula a revogação concorrente após a primeira consulta, antes de adquirir a trava de escrita.
    private sealed class RevogarAntesDaTrava(Func<Task> revogar) : DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            await revogar();
            return result;
        }
    }
}
