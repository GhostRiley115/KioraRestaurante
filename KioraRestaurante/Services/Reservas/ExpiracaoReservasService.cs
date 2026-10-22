namespace KioraRestaurante.Services.Reservas;
// Expiração automática libera reservas não atendidas; mesas ocupadas exigem ação da recepção.
public class ExpiracaoReservasService(IServiceScopeFactory scopes,ILogger<ExpiracaoReservasService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));
        try { while(await timer.WaitForNextTickAsync(ct))
        {
            try { using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<ReservaService>().Manutencao(ct); }
            catch(Exception ex) when(!ct.IsCancellationRequested) {logger.LogWarning("Falha na manutenção de reservas: {Tipo}.",ex.GetType().Name);}
        }}catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
    }
}
