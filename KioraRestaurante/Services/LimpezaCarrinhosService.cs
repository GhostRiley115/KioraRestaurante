using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services;

//BackgroundService significa que esse serviço roda em segundo plano.
public class LimpezaCarrinhosService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    //Cria um log da funcionalidade de remover carrinho.
    private readonly ILogger<LimpezaCarrinhosService> _logger;

    public LimpezaCarrinhosService(IServiceScopeFactory scopeFactory,
        ILogger<LimpezaCarrinhosService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //Faz consultas no banco de uma em uma hora.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            do
            {
                try
                {
                    //Abre sessão -> Pega DbContext -> Faz consulta -> Termina sessão
                    using var scope = _scopeFactory.CreateScope();

                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var agora = DateTime.UtcNow;

                    //Consulta carrinhos que já foram expirados e remove do banco.
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