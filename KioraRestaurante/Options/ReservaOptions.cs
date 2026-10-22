namespace KioraRestaurante.Options;

// Minutos contados desde 00h. O encerramento 1440 representa a meia-noite seguinte.
public class ReservaOptions
{
    public int AberturaMinutos { get; set; } = 11 * 60;
    public int UltimaReservaMinutos { get; set; } = 23 * 60 + 20;
    public int FechamentoMinutos { get; set; } = 24 * 60;
    public int IntervaloMinutos { get; set; } = 20;
    public int PermanenciaMinutos { get; set; } = 90;
    public int ToleranciaMinutos { get; set; } = 15;
    public int ChamadaMinutos { get; set; } = 10;
    public int AntecedenciaDias { get; set; } = 30;
}
