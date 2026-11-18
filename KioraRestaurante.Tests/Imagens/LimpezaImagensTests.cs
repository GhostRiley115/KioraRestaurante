using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services.Imagens;
using KioraRestaurante.Services.Produtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KioraRestaurante.Tests;

public class LimpezaImagensTests
{
    public static IEnumerable<object[]> Referencias()
    {
        foreach (var origem in new[] { "ativo", "inativo", "delivery", "salao" })
        foreach (var formato in new[] { "id", "url", "transformada", "sem-extensao", "sem-extensao-consulta", "sem-extensao-fragmento" })
            yield return [origem, formato];
    }

    [Theory, MemberData(nameof(Referencias))]
    public async Task ReferenciaAtualOuHistoricaImpedeExclusao(string origem, string formato)
    {
        await using var b = new Banco();
        var publicId = Banco.PublicId();
        var original = Banco.Url(publicId);
        var url = formato switch
        {
            "transformada" => $"https://res.cloudinary.com/ficticio/image/upload/c_fill,w_200/v12/{publicId}.webp",
            "sem-extensao-consulta" => $"https://res.cloudinary.com/ficticio/image/upload/c_fill,w_200/v12/{publicId}?cache=1",
            "sem-extensao-fragmento" => $"https://res.cloudinary.com/ficticio/image/upload/v12/{publicId}#foto",
            "sem-extensao" => new Cloudinary(new Account("ficticio", "nao-usada", "nao-usado"))
                .Api.UrlImgUp.Secure(true).BuildUrl(publicId),
            _ => original
        };
        await b.Referenciar(origem, formato == "id" ? publicId : null, url);
        b.Db.Add(new LimpezaImagemPendente { PublicId = publicId, Url = original, ProximaTentativaUtc = DateTime.UtcNow.AddMinutes(-1) });
        await b.Db.SaveChangesAsync();

        await b.Worker.ProcessarLote();

        Assert.Empty(b.Imagens.Excluidas);
        Assert.Empty(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task OrfaEhExcluidaMasImagemComIdentificadorParecidoContinuaReferenciada()
    {
        await using var b = new Banco();
        var publicId = Banco.PublicId();
        await b.Referenciar("ativo", null, Banco.Url(publicId + "extra"));
        var tarefa = b.Agendar(publicId);
        await b.Db.SaveChangesAsync();

        await b.Worker.ProcessarLote();

        Assert.Equal([publicId], b.Imagens.Excluidas);
        Assert.False(await b.Db.LimpezasImagensPendentes.AsNoTracking().AnyAsync(x => x.Id == tarefa.Id));
        Assert.Contains(publicId + "extra", (await b.Db.Produtos.AsNoTracking().SingleAsync()).Imagem);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, null)]
    public async Task DesativadaOuSimulacaoNuncaExcluiImagem(bool ativada, bool? simular)
    {
        await using var b = new Banco(ativada, simular);
        b.Agendar(); await b.Db.SaveChangesAsync();
        await b.Worker.ProcessarLote();
        Assert.Empty(b.Imagens.Excluidas);
        Assert.Single(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("kiora/home/logo")]
    [InlineData("outra-pasta/01234567890123456789012345678901")]
    [InlineData("kiora/produtos/../../logo")]
    public async Task IdentificadorForaDaPastaGerenciadaNuncaEhEnviadoParaExclusao(string publicId)
    {
        await using var b = new Banco();
        b.Agendar(publicId); await b.Db.SaveChangesAsync();
        await b.Worker.ProcessarLote();
        Assert.Empty(b.Imagens.Excluidas);
        var pendente = await b.Db.LimpezasImagensPendentes.AsNoTracking().SingleAsync();
        Assert.True(pendente.ProximaTentativaUtc > DateTime.UtcNow.AddHours(23));
        Assert.False(await b.Imagens.Excluir(publicId));
        Assert.Empty(b.Imagens.Excluidas);
    }

    [Fact]
    public async Task FalhaDeExclusaoAguardaRetryEImagemJaExcluidaPodeSerConferidaNovamente()
    {
        await using var b = new Banco();
        var tarefa = b.Agendar(); await b.Db.SaveChangesAsync();
        b.Imagens.ResultadoExclusao = "error";
        await b.Worker.ProcessarLote();
        var falha = await b.Db.LimpezasImagensPendentes.AsNoTracking().SingleAsync();
        Assert.Equal(1, falha.Tentativas);
        Assert.InRange(falha.ProximaTentativaUtc, DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow.AddMinutes(3));
        await b.Worker.ProcessarLote();
        Assert.Single(b.Imagens.Excluidas);

        b.Db.ChangeTracker.Clear();
        await b.Db.LimpezasImagensPendentes.Where(x => x.Id == tarefa.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(i => i.ProximaTentativaUtc, DateTime.UtcNow.AddSeconds(-1)));
        b.Imagens.ResultadoExclusao = "not found";
        await b.Worker.ProcessarLote();
        Assert.Equal(2, b.Imagens.Excluidas.Count);
        Assert.Empty(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalhaAoGravarProdutoPreservaReferenciaAnteriorEApenasNovaOrfaVaiParaLimpeza(bool editar)
    {
        await using var b = new Banco();
        var antigo = Banco.PublicId();
        await b.Referenciar("ativo", antigo, Banco.Url(antigo));
        b.Falha.FalharProduto = true;
        var produtos = new ProdutoService(b.Db, b.Imagens, NullLogger<ProdutoService>.Instance);
        if (editar)
            await Assert.ThrowsAsync<DbUpdateException>(() => produtos.Editar(b.Admin.Id, b.Produto.Id,
                new EditarProdutoRequestDTO { Nome = "Alterado", Descricao = "Descrição", Preco = 20, CategoriaId = 2 }, Banco.Foto()));
        else
            await Assert.ThrowsAsync<DbUpdateException>(() => produtos.Criar(b.Admin.Id,
                new CriarProdutoRequestDTO { Nome = "Novo", Descricao = "Descrição", Preco = 20, CategoriaId = 2 }, Banco.Foto()));

        b.Db.ChangeTracker.Clear();
        var salvo = await b.Db.Produtos.AsNoTracking().SingleAsync();
        Assert.Equal(antigo, salvo.ImagemPublicId);
        var orfa = await b.Db.LimpezasImagensPendentes.AsNoTracking().SingleAsync();
        Assert.Equal(b.Imagens.Enviadas.Single(), orfa.PublicId);
        Assert.NotEqual(antigo, orfa.PublicId);
        Assert.True(orfa.ProximaTentativaUtc > DateTime.UtcNow.AddHours(23));
        await b.Worker.ProcessarLote();
        Assert.Empty(b.Imagens.Excluidas);
        await b.Db.LimpezasImagensPendentes.ExecuteUpdateAsync(x => x.SetProperty(i => i.ProximaTentativaUtc, DateTime.UtcNow.AddSeconds(-1)));
        await b.Worker.ProcessarLote();
        Assert.Equal([orfa.PublicId], b.Imagens.Excluidas);
        Assert.Equal(antigo, (await b.Db.Produtos.AsNoTracking().SingleAsync()).ImagemPublicId);
    }

    [Fact]
    public async Task EdicaoConfirmadaAgendaSomenteImagemAntigaENaoApagaANova()
    {
        await using var b = new Banco();
        var antigo = Banco.PublicId(); await b.Referenciar("inativo", antigo, Banco.Url(antigo));
        var produtos = new ProdutoService(b.Db, b.Imagens, NullLogger<ProdutoService>.Instance);
        await produtos.Editar(b.Admin.Id, b.Produto.Id,
            new EditarProdutoRequestDTO { Nome = "Alterado", Descricao = "Descrição", Preco = 20, CategoriaId = 2 }, Banco.Foto());
        var tarefas = await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync();
        Assert.Equal(2, tarefas.Count);
        Assert.Contains(tarefas, x => x.PublicId == antigo && x.ProximaTentativaUtc < DateTime.UtcNow.AddMinutes(6));
        var novo = (await b.Db.Produtos.AsNoTracking().SingleAsync()).ImagemPublicId;
        Assert.Equal(b.Imagens.Enviadas.Single(), novo);
        await b.Db.LimpezasImagensPendentes.ExecuteUpdateAsync(x => x.SetProperty(i => i.ProximaTentativaUtc, DateTime.UtcNow.AddSeconds(-1)));
        await b.Worker.ProcessarLote();
        Assert.Equal([antigo], b.Imagens.Excluidas);
        Assert.Empty(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TimeoutDoUploadPreservaTarefaDeConferenciaSemAlterarCatalogo()
    {
        await using var b = new Banco();
        b.Imagens.TimeoutUpload = true;
        await Assert.ThrowsAsync<RegraProdutoException>(() => b.Imagens.Enviar(Banco.Foto()));
        var pendente = await b.Db.LimpezasImagensPendentes.AsNoTracking().SingleAsync();
        Assert.Equal(b.Imagens.Enviadas.Single(), pendente.PublicId);
        Assert.True(pendente.ProximaTentativaUtc > DateTime.UtcNow.AddHours(23));
        Assert.Empty(b.Imagens.Excluidas);
        Assert.Null((await b.Db.Produtos.AsNoTracking().SingleAsync()).ImagemPublicId);
    }

    [Fact]
    public async Task FalhaAoRegistrarConferenciaImpedeUploadSemRastreamento()
    {
        await using var b = new Banco();
        b.Falha.FalharFila = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => b.Imagens.Enviar(Banco.Foto()));
        Assert.Empty(b.Imagens.Enviadas);
        Assert.Empty(await b.Db.LimpezasImagensPendentes.AsNoTracking().ToListAsync());
    }

    private sealed class Banco : IAsyncDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly ServiceProvider _provider;
        public AppDbContext Db { get; }
        public Usuario Admin { get; } = new() { Nome = "Admin", Email = "admin@example.test", SenhaHash = "nao-utilizada", Tipo = TipoUsuario.Administrador };
        public Produto Produto { get; } = new() { Nome = "Prato", Descricao = "Descrição", Preco = 20, CategoriaId = 2 };
        public FalhaGravacao Falha { get; } = new();
        public ImagensSemRede Imagens { get; }
        public LimpezaImagensService Worker { get; }
        public Banco(bool ativada = true, bool? simular = false)
        {
            _conexao.Open();
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).AddInterceptors(Falha).Options);
            Db.Database.EnsureCreated(); Db.AddRange(Admin, Produto); Db.SaveChanges();
            var dados = new Dictionary<string, string?>
            { ["LimpezaImagens:Ativada"] = ativada.ToString(), ["Cloudinary:CloudName"] = "ficticio", ["Cloudinary:ApiKey"] = "nao-usada", ["Cloudinary:ApiSecret"] = "nao-usado" };
            if (simular.HasValue) dados["LimpezaImagens:Simular"] = simular.Value.ToString();
            var config = new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexao).AddInterceptors(Falha));
            services.AddSingleton<ImagensSemRede>(sp => new(config, sp.GetRequiredService<IServiceScopeFactory>()));
            services.AddSingleton<IImagemProdutoService>(sp => sp.GetRequiredService<ImagensSemRede>());
            _provider = services.BuildServiceProvider();
            Imagens = _provider.GetRequiredService<ImagensSemRede>();
            Worker = new(_provider.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<LimpezaImagensService>.Instance);
        }
        public LimpezaImagemPendente Agendar(string? publicId = null)
        {
            var tarefa = new LimpezaImagemPendente { PublicId = publicId ?? PublicId(), ProximaTentativaUtc = DateTime.UtcNow.AddMinutes(-1) };
            Db.Add(tarefa); return tarefa;
        }
        public async Task Referenciar(string origem, string? publicId, string url)
        {
            if (origem is "ativo" or "inativo")
            { Produto.Imagem = url; Produto.ImagemPublicId = publicId; Produto.Ativo = origem == "ativo"; }
            else if (origem == "delivery")
                Db.Add(new Pedido { UsuarioId = Admin.Id, StatusPedido = StatusPedido.Entregue,
                    ItensPedido = [new() { ProdutoId = Produto.Id, NomeProduto = Produto.Nome, Quantidade = 1, PrecoUnitario = 20, ImagemUrl = url, ImagemPublicId = publicId }] });
            else
                Db.Add(new AtendimentoMesa { MesaId = 1, GarcomId = Admin.Id, Situacao = SituacaoAtendimento.Encerrado,
                    Itens = [new() { ProdutoId = Produto.Id, NomeProduto = Produto.Nome, Quantidade = 1, PrecoUnitario = 20, ImagemUrl = url, ImagemPublicId = publicId }] });
            await Db.SaveChangesAsync();
        }
        public static string PublicId() => "kiora/produtos/" + Guid.NewGuid().ToString("N");
        public static string Url(string publicId) => $"https://res.cloudinary.com/ficticio/image/upload/v1/{publicId}.webp";
        public static IFormFile Foto() => new FormFile(new MemoryStream([1]), 0, 1, "foto", "foto.webp");
        public async ValueTask DisposeAsync()
        { Worker.Dispose(); await Db.DisposeAsync(); await _provider.DisposeAsync(); await _conexao.DisposeAsync(); }
    }
    private sealed class ImagensSemRede(IConfiguration config, IServiceScopeFactory scopes)
        : ImagemProdutoService(config, NullLogger<ImagemProdutoService>.Instance, scopes)
    {
        public List<string> Enviadas { get; } = [];
        public List<string> Excluidas { get; } = [];
        public string ResultadoExclusao { get; set; } = "ok";
        public bool TimeoutUpload { get; set; }
        protected override Task<ImageUploadResult> EnviarAoProvedor(Cloudinary cliente, ImageUploadParams parametros)
        {
            Enviadas.Add(parametros.PublicId);
            if (TimeoutUpload) throw new TaskCanceledException("Timeout fictício, sem HTTP.");
            return Task.FromResult(new ImageUploadResult { PublicId = parametros.PublicId, SecureUrl = new Uri(Banco.Url(parametros.PublicId)) });
        }
        protected override Task<DeletionResult> ExcluirDoProvedor(Cloudinary cliente, DeletionParams parametros)
        { Excluidas.Add(parametros.PublicId); return Task.FromResult(new DeletionResult { Result = ResultadoExclusao }); }
    }
    private sealed class FalhaGravacao : SaveChangesInterceptor
    {
        public bool FalharProduto { get; set; }
        public bool FalharFila { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (FalharFila && eventData.Context!.ChangeTracker.Entries<LimpezaImagemPendente>().Any(e => e.State == EntityState.Added))
                throw new DbUpdateException("Falha simulada ao registrar conferência.");
            if (FalharProduto && eventData.Context!.ChangeTracker.Entries<Produto>().Any(e => e.State is EntityState.Added or EntityState.Modified))
                throw new DbUpdateException("Falha simulada ao gravar produto.");
            return ValueTask.FromResult(result);
        }
    }
}
