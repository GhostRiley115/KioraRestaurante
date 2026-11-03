namespace KioraRestaurante.Services.Cardapio;

public sealed record PeriodoMaisPedidos(
    DateOnly InicioLocal,
    DateOnly FimLocalExclusivo,
    DateTime InicioUtc,
    DateTime FimUtc)
{
    // Segunda-feira escolhida como referência dos ciclos de 14 dias.
    // Use a mesma data no Rider e no Render; não a altere em cada publicação.
    private static readonly DateOnly DataBase = new(2026, 11, 2);
    private static readonly TimeZoneInfo Fuso =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static PeriodoMaisPedidos Atual(DateTimeOffset agora)
    {
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora, Fuso).DateTime);
        var dias = hoje.DayNumber - DataBase.DayNumber;
        var ciclo = (int)Math.Floor(dias / 14d);
        var inicio = DataBase.AddDays(ciclo * 14);
        var fim = inicio.AddDays(14);

        return new PeriodoMaisPedidos(
            inicio,
            fim,
            TimeZoneInfo.ConvertTimeToUtc(inicio.ToDateTime(TimeOnly.MinValue), Fuso),
            TimeZoneInfo.ConvertTimeToUtc(fim.ToDateTime(TimeOnly.MinValue), Fuso));
    }
}
