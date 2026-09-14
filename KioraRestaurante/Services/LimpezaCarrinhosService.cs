using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services;

public class LimpezaCarrinhosService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LimpezaCarrinhosService> _logger;

    public LimpezaCarrinhosService(IServiceScopeFactory scopeFactory,
        ILogger<LimpezaCarrinhosService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var agora = DateTime.UtcNow;

                    await context.Carrinhos.Where
                        (c =>
                            c.UsuarioId == null
                            && c.ExpiraEmUtc != null
                            && c.ExpiraEmUtc <= agora)
                        .ExecuteDeleteAsync(stoppingToken);
                }
                catch(Exception ex)
                    when(!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "falha ao limpar carrinhos expirados.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken)) ;
        }
        catch(OperationCanceledException)
            when(stoppingToken.IsCancellationRequested)
        {

        }
    }
}