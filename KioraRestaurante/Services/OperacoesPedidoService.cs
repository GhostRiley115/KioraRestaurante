using KioraRestaurante.Data;
using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services;

// Retoma somente solicitações já confirmadas pelo cliente, inclusive depois de reiniciar o servidor.
public class OperacoesPedidoService(IServiceScopeFactory scopes, ILogger<OperacoesPedidoService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try
                {
                    await using var consulta = scopes.CreateAsyncScope();
                    var db = consulta.ServiceProvider.GetRequiredService<AppDbContext>();
                    var ids = await db.OperacoesPedido.AsNoTracking().Where(o => o.Estado != EstadoOperacaoPedido.Concluida
                        && o.Estado != EstadoOperacaoPedido.Desfeita).OrderBy(o => o.ConferidaEmUtc).Select(o => o.Id).Take(20).ToListAsync(ct);
                    foreach (var id in ids)
                    {
                        await using var escopo = scopes.CreateAsyncScope();
                        await escopo.ServiceProvider.GetRequiredService<GerenciarPedidoService>().Processar(id, ct);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("Conferência de alterações de pedido: {Tipo}.", ex.GetType().Name); }
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
