using KioraRestaurante.Data;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KioraRestaurante.Tests;

public class ReservasConfiguracaoTests
{
    private static HorariosReserva Horarios() => new(Microsoft.Extensions.Options.Options.Create(new ReservaOptions()), new Relogio());

    [Theory]
    [InlineData("11:00", 90)]
    [InlineData("22:20", 90)]
    [InlineData("23:20", 40)]
    public void HorariosRespeitamBrasiliaEFechamento(string hora, int minutos)
    {
        var horarios = Horarios();
        var (inicio, fim) = horarios.Periodo(horarios.Hoje.AddDays(1), hora);
        Assert.Equal(minutos, (fim-inicio).TotalMinutes);
        Assert.Equal(hora, HorariosReserva.Local(inicio).ToString("HH:mm"));
        Assert.True(fim <= HorariosReserva.Utc(horarios.Hoje.AddDays(1), 1440));
    }

    [Theory]
    [InlineData("10:40")]
    [InlineData("23:40")]
    [InlineData("12:15")]
    [InlineData("inválido")]
    public void RejeitaHorariosForaDaAgenda(string hora) => Assert.Throws<RegraReservaException>(() => Horarios().Periodo(Horarios().Hoje.AddDays(1), hora));

    [Theory]
    [InlineData(12, 0, 90)]
    [InlineData(22, 29, 90)]
    [InlineData(23, 20, 40)]
    public void FilaUsaMesmoInstanteParaCalcularTempoDisponivel(int hora, int minuto, int duracao)
    {
        var horarios = Horarios();
        var agora = HorariosReserva.Utc(horarios.Hoje, hora * 60 + minuto).AddMilliseconds(10);
        var minutos = (horarios.FimAtendimentoAgora(agora) - agora).TotalMinutes;
        if (duracao == 90) Assert.Equal(90, minutos);
        else Assert.InRange(minutos, duracao - .001, duracao);
    }

    [Fact]
    public void SnapshotIncluiReservasEPreservaModeloAtual()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=localhost;Database=teste;User ID=teste", new MySqlServerVersion(new Version(8,0,36))).Options;
        using var db = new AppDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains("20261003023811_AdicionarReservasMesasEFila", db.Database.GetMigrations());
    }

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,10,21,15,0,0,TimeSpan.Zero);
    }
}
