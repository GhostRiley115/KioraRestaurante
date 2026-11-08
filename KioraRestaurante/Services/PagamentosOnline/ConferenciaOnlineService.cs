using KioraRestaurante.Data;
using KioraRestaurante.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace KioraRestaurante.Services.PagamentosOnline;
public class ConferenciaOnlineService(IServiceScopeFactory scopes, IOptions<MercadoPagoConfig> options,
    ILogger<ConferenciaOnlineService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            do
            {
                if (!options.Value.Ativado) continue;
                try
                {
                    await using var consulta = scopes.CreateAsyncScope();
                    var db = consulta.ServiceProvider.GetRequiredService<AppDbContext>();
                    var limite = DateTime.UtcNow.AddDays(-2);
                    var ids = await db.PagamentosOnline.AsNoTracking().Where(p => p.OrderId != null
                        && (p.CriadoEmUtc >= limite || p.Pedido.StatusPagamento == Models.Enums.StatusPagamento.Pendente))
                        .OrderBy(p => p.ConferidoEmUtc).Select(p => p.PedidoId).Take(20).ToListAsync(ct);
                    foreach (var id in ids)
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var banco = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var orderId = await banco.PagamentosOnline.Where(p => p.PedidoId == id).Select(p => p.OrderId).SingleAsync(ct);
                        try { await scope.ServiceProvider.GetRequiredService<CheckoutOnlineService>().Sincronizar(orderId!, ct); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception ex) { logger.LogWarning("Conferência do pedido {Id}: {Tipo}.", id, ex.GetType().Name); }
                        // Também avança a fila quando não houve mudança externa ou ocorreu erro.
                        await banco.PagamentosOnline.Where(p => p.PedidoId == id)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConferidoEmUtc, DateTime.UtcNow), ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("Ciclo de conferência: {Tipo}.", ex.GetType().Name); }
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
