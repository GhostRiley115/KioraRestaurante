using KioraRestaurante.Data;
using KioraRestaurante.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Imagens;

public class LimpezaImagensService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<LimpezaImagensService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            do
            {
                if (!configuration.GetValue<bool>("LimpezaImagens:Ativada"))
                    continue;

                try { await ProcessarLote(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { throw; }
                catch (Exception ex)
                {
                    // A fila permanece no banco para a próxima execução.
                    logger.LogWarning("Falha no ciclo de limpeza: {Tipo}.",
                        ex.GetType().Name);
                }
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task ProcessarLote(CancellationToken ct)
    {
        await using var consulta = scopes.CreateAsyncScope();
        var db = consulta.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;
        var ids = await db.LimpezasImagensPendentes.AsNoTracking()
            .Where(x => x.ProximaTentativaUtc <= agora)
            .OrderBy(x => x.ProximaTentativaUtc)
            .Select(x => x.Id).Take(20).ToListAsync(ct);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try { await Processar(id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Limpeza {Id} será retomada: {Tipo}.",
                    id, ex.GetType().Name);
            }
        }
    }

    private async Task Processar(Guid id, CancellationToken ct)
    {
        // Contexto independente da requisição que cadastrou/editou o produto.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.LimpezasImagensPendentes.SingleOrDefaultAsync(
            x => x.Id == id, ct);
        if (item == null || item.ProximaTentativaUtc > DateTime.UtcNow) return;

        if (!IdentificadorImagemProduto.Gerenciado(item.PublicId))
        {
            logger.LogWarning("Identificador fora do padrão; revisar manualmente: {PublicId}.", item.PublicId);
            item.ProximaTentativaUtc = DateTime.UtcNow.AddDays(1);
            await db.SaveChangesAsync(ct);
            return;
        }

        // Produtos inativos também precisam preservar suas imagens.
        // O trecho da URL cobre URLs Cloudinary com versão/transformação e extensão.
        var trechoUrl = "/" + item.PublicId + ".";
        var emUso = await db.Produtos.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(p => p.ImagemPublicId == item.PublicId
                || (item.Url != null && p.Imagem == item.Url)
                || p.Imagem.Contains(trechoUrl), ct);

        if (emUso)
        {
            // Remove somente a tarefa; preserva a imagem no Cloudinary.
            db.LimpezasImagensPendentes.Remove(item);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (configuration.GetValue("LimpezaImagens:Simular", true))
        {
            logger.LogInformation("SIMULAÇÃO: imagem sem referência seria excluída: {PublicId}.", item.PublicId);
            return;
        }

        var imagens = scope.ServiceProvider.GetRequiredService<IImagemProdutoService>();
        ct.ThrowIfCancellationRequested();
        var concluido = await imagens.Excluir(item.PublicId);
        if (concluido)
        {
            db.LimpezasImagensPendentes.Remove(item);
        }
        else
        {
            item.Tentativas++;
            // Aguarda entre 2 e 60 minutos antes da próxima tentativa.
            item.ProximaTentativaUtc = DateTime.UtcNow.AddMinutes(
                Math.Min(60, Math.Pow(2, Math.Min(item.Tentativas, 6))));
        }
        await db.SaveChangesAsync(ct);
    }
}