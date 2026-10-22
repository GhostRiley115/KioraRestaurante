using System.Globalization;
using KioraRestaurante.Models;
using KioraRestaurante.Options;
using Microsoft.Extensions.Options;
namespace KioraRestaurante.Services.Reservas;

public class HorariosReserva(IOptions<ReservaOptions> options, TimeProvider tempo)
{
    public ReservaOptions Regras => options.Value;
    public DateTime Agora => tempo.GetUtcNow().UtcDateTime;
    public static TimeZoneInfo Fuso => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public DateOnly Hoje => DateOnly.FromDateTime(Local(Agora));
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),Fuso);
    public static DateTime Utc(DateOnly data,int minutos) => TimeZoneInfo.ConvertTimeToUtc(data.ToDateTime(TimeOnly.MinValue).AddMinutes(minutos),Fuso);
    public static string Hora(int minutos) => minutos == 1440 ? "00:00" : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutos)).ToString("HH:mm");
    public void ValidarData(DateOnly data)
    {
        if(data < Hoje || data > Hoje.AddDays(Regras.AntecedenciaDias))
            throw new RegraReservaException($"Escolha uma data entre hoje e os próximos {Regras.AntecedenciaDias} dias.");
    }
    public (DateTime inicio, DateTime fim) Periodo(DateOnly data,string? horario)
    {
        ValidarData(data);
        if(!TimeOnly.TryParseExact(horario,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var hora))
            throw new RegraReservaException("Escolha um horário válido.");
        var minutos=hora.Hour*60+hora.Minute;
        if(minutos<Regras.AberturaMinutos || minutos>Regras.UltimaReservaMinutos
            || (minutos-Regras.AberturaMinutos)%Regras.IntervaloMinutos!=0)
            throw new RegraReservaException("Este horário não faz parte da agenda de reservas.");
        var inicio=Utc(data,minutos);
        if(inicio<Agora) throw new RegraReservaException("Este horário já passou.");
        return (inicio,Utc(data,Math.Min(minutos+Regras.PermanenciaMinutos,Regras.FechamentoMinutos)));
    }
    public DateTime FimAtendimentoAgora(DateTime? agora = null)
    {
        // Reutiliza o mesmo instante para não perder milissegundos entre duas leituras do relógio.
        var instante = agora ?? Agora;
        var dia = DateOnly.FromDateTime(Local(instante));
        return new[]{instante.AddMinutes(Regras.PermanenciaMinutos),Utc(dia,Regras.FechamentoMinutos)}.Min();
    }
    public void ExigirFilaAberta()
    {
        var local=Local(Agora);var minutos=local.Hour*60+local.Minute;
        if(minutos<Regras.AberturaMinutos || minutos>Regras.UltimaReservaMinutos)
            throw new RegraReservaException("A fila funciona das 11h às 23h20, no horário de Brasília.");
    }
    public static string Texto(SituacaoReserva status)=>status switch {
        SituacaoReserva.Confirmada=>"Reserva confirmada",SituacaoReserva.Aguardando=>"Aguardando na fila",
        SituacaoReserva.Chamada=>"É a sua vez",SituacaoReserva.EmAtendimento=>"Na mesa",
        SituacaoReserva.Concluida=>"Atendimento encerrado",SituacaoReserva.Cancelada=>"Cancelada",_=>"Prazo encerrado"};
}
