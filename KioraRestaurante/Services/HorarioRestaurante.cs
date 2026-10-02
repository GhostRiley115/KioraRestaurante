namespace KioraRestaurante.Services;

// O banco guarda UTC; as telas e os filtros usam o dia local do restaurante.
public static class HorarioRestaurante
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateTime ParaLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);
    public static DateTime InicioDiaUtc(DateTime dia) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dia.Date, DateTimeKind.Unspecified), Fuso);
}
