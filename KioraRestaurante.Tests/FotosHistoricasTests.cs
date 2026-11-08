using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Imagens;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Banco = KioraRestaurante.Tests.CheckoutEntregaTests.Banco;

namespace KioraRestaurante.Tests;

public class FotosHistoricasTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task LimpezaPreservaFotoDeCompraMesmoSemReferenciaAtualNoCatalogo(bool comPublicId)
    {
        using var b = new Banco(); var produto = await b.Db.Produtos.SingleAsync();
        var publicId = "kiora/produtos/" + Guid.NewGuid().ToString("N");
        var url = "https://res.cloudinary.com/ficticio/image/upload/v1/" + publicId + ".webp";
        produto.Imagem = url; produto.ImagemPublicId = comPublicId ? publicId : null; await b.Db.SaveChangesAsync();
        await b.Pedidos.ConfirmarPedido(b.Acesso, await b.Preparar(false));
        produto.Imagem = "https://example.org/substituida.webp"; produto.ImagemPublicId = null;
        b.Db.Add(new LimpezaImagemPendente { PublicId = publicId, Url = comPublicId ? null : url, ProximaTentativaUtc = DateTime.UtcNow.AddSeconds(-1) });
        await b.Db.SaveChangesAsync();
        var imagens = new ImagensFalsas();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LimpezaImagens:Ativada"] = "true", ["LimpezaImagens:Simular"] = "false" }).Build();
        var gravacao = new SinalGravacao();
        var services = new ServiceCollection(); services.AddDbContext<AppDbContext>(o => o.UseSqlite(b.Db.Database.GetDbConnection()).AddInterceptors(gravacao));
        services.AddSingleton<IImagemProdutoService>(imagens);
        await using var provider = services.BuildServiceProvider();
        using var worker = new LimpezaImagensService(provider.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<LimpezaImagensService>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await gravacao.Concluida.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, imagens.Exclusoes); Assert.Empty(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
        Assert.Equal(url, (await b.Db.ItensPedido.AsNoTracking().SingleAsync()).ImagemUrl);
    }
    private class SinalGravacao : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public TaskCompletionSource Concluida { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        { Concluida.TrySetResult(); return ValueTask.FromResult(result); }
    }
    private class ImagensFalsas : IImagemProdutoService
    {
        public int Exclusoes { get; private set; }
        public Task<ImagemProdutoResultado> Enviar(IFormFile foto) => throw new NotSupportedException();
        public Task<bool> Excluir(string publicId) { Exclusoes++; return Task.FromResult(true); }
    }
}
