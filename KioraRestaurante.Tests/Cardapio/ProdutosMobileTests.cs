using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using KioraRestaurante.Controllers.Api;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KioraRestaurante.Services.Imagens;
using KioraRestaurante.Services.Produtos;

namespace KioraRestaurante.Tests;

public class ProdutosMobileTests
{
    [Theory]
    [InlineData("ativo")]
    [InlineData("disponibilidade")]
    [InlineData("desconto")]
    public async Task PatchSemCampoObrigatorioNaoMudaProduto(string operacao)
    {
        using var b = new BancoProdutosMobile();
        var api = b.Administracao();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        object dto = operacao switch
        {
            "ativo" => JsonSerializer.Deserialize<AlterarStatusProdutoRequestDTO>("{}", json)!,
            "disponibilidade" => JsonSerializer.Deserialize<AlterarDisponibilidadeProdutoRequestDTO>("{}", json)!,
            _ => JsonSerializer.Deserialize<AlterarDescontoProdutoApiRequestDTO>("{}", json)!
        };
        Assert.False(Validator.TryValidateObject(dto, new(dto), [], true));
        var resultado = operacao switch
        {
            "ativo" => await api.AlterarAtivo(b.Produto.Id, (AlterarStatusProdutoRequestDTO)dto),
            "disponibilidade" => await api.AlterarDisponibilidade(b.Produto.Id, (AlterarDisponibilidadeProdutoRequestDTO)dto),
            _ => await api.AlterarDesconto(b.Produto.Id, (AlterarDescontoProdutoApiRequestDTO)dto)
        };
        Assert.IsType<BadRequestObjectResult>(resultado);
        var persistido = await b.Db.Produtos.AsNoTracking().SingleAsync();
        Assert.True(persistido.Ativo);
        Assert.True(persistido.Disponivel);
        Assert.Equal(10m, persistido.DescontoPercentual);
    }

    [Theory]
    [InlineData("ativo")]
    [InlineData("disponibilidade")]
    [InlineData("desconto")]
    public async Task FalseOuZeroExplicitosContinuamPermitidos(string operacao)
    {
        using var b = new BancoProdutosMobile();
        var api = b.Administracao();
        var resultado = operacao switch
        {
            "ativo" => await api.AlterarAtivo(b.Produto.Id, new() { Ativo = false }),
            "disponibilidade" => await api.AlterarDisponibilidade(b.Produto.Id, new() { Disponivel = false }),
            _ => await api.AlterarDesconto(b.Produto.Id, new() { Percentual = 0 })
        };
        Assert.IsType<OkObjectResult>(resultado);
        var persistido = await b.Db.Produtos.AsNoTracking().SingleAsync();
        if (operacao == "ativo") { Assert.False(persistido.Ativo); Assert.False(persistido.Disponivel); }
        if (operacao == "disponibilidade") Assert.False(persistido.Disponivel);
        if (operacao == "desconto") Assert.Equal(0m, persistido.DescontoPercentual);
    }

    [Theory]
    [InlineData("categoria")]
    [InlineData("preco")]
    [InlineData("combo")]
    [InlineData("foto")]
    public async Task RegrasDeCadastroRetornam400SemCriarProduto(string erro)
    {
        using var b = new BancoProdutosMobile();
        var dto = new CriarProdutoRequestDTO
        { Nome = "Novo produto", Descricao = "Descrição", Preco = 20m, CategoriaId = 2, Disponivel = true };
        if (erro == "categoria") dto.CategoriaId = 999;
        if (erro == "preco") dto.Preco = 19.999m;
        if (erro == "combo") dto.EhCombo = true;
        if (erro == "foto") b.Imagens.Recusar = true;
        var resultado = await b.Administracao().CadastrarProduto(dto, BancoProdutosMobile.Foto());
        var resposta = Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.NotNull(resposta.Value);
        Assert.Single(await b.Db.Produtos.AsNoTracking().ToListAsync());
        Assert.Equal(erro == "foto" ? 1 : 0, b.Imagens.Envios);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public async Task PercentualInvalidoNaoSubstituiDescontoAtual(int percentual)
    {
        using var b = new BancoProdutosMobile();
        var resultado = await b.Administracao().AlterarDesconto(b.Produto.Id, new() { Percentual = percentual });
        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(10m, (await b.Db.Produtos.AsNoTracking().SingleAsync()).DescontoPercentual);
    }

    [Theory]
    [InlineData("ativo")]
    [InlineData("disponibilidade")]
    [InlineData("desconto")]
    [InlineData("cadastro")]
    public async Task FalhaDeGravacaoRetornaConflitoSemConfirmarAlteracao(string operacao)
    {
        using var b = new BancoProdutosMobile();
        var api = b.Administracao();
        b.Gravacao.Falhar = true;
        IActionResult resultado = operacao switch
        {
            "ativo" => await api.AlterarAtivo(b.Produto.Id, new() { Ativo = false }),
            "disponibilidade" => await api.AlterarDisponibilidade(b.Produto.Id, new() { Disponivel = false }),
            "desconto" => await api.AlterarDesconto(b.Produto.Id, new() { Percentual = 0 }),
            _ => (await api.CadastrarProduto(new()
            { Nome = "Novo", Descricao = "Descrição", Preco = 20m, CategoriaId = 2 }, BancoProdutosMobile.Foto())).Result!
        };
        Assert.IsType<ConflictObjectResult>(resultado);
        var persistido = Assert.Single(await b.Db.Produtos.AsNoTracking().ToListAsync());
        Assert.True(persistido.Ativo);
        Assert.True(persistido.Disponivel);
        Assert.Equal(10m, persistido.DescontoPercentual);
    }
}

internal sealed class BancoProdutosMobile : IDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    public AppDbContext Db { get; }
    public Usuario Administrador { get; }
    public Produto Produto { get; }
    public ImagensMobile Imagens { get; } = new();
    public FalhaGravacaoMobile Gravacao { get; } = new();
    public ProdutoService Produtos { get; }
    public BancoProdutosMobile()
    {
        _conexao.Open();
        Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).AddInterceptors(Gravacao).Options);
        Db.Database.EnsureCreated();
        Administrador = new() { Nome = "Administrador", Email = "admin@example.com", SenhaHash = "não utilizada", Tipo = TipoUsuario.Administrador };
        Produto = new() { Nome = "Lámen da casa", Descricao = "Descrição", CategoriaId = 2, Preco = 40m, DescontoPercentual = 10m };
        Db.AddRange(Administrador, Produto);
        Db.SaveChanges();
        Produtos = new(Db, Imagens, NullLogger<ProdutoService>.Instance);
    }
    public ProdutosAdminApiController Administracao() => new(Produtos)
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, Administrador.Id.ToString()), new(ClaimTypes.Role, "Administrador")], "Teste")) } }
    };
    public static IFormFile Foto() => new FormFile(new MemoryStream([1]), 0, 1, "foto", "produto.png");
    public void Dispose() { Db.Dispose(); _conexao.Dispose(); }
}

internal sealed class ImagensMobile : IImagemProdutoService
{
    public int Envios { get; private set; }
    public bool Recusar { get; set; }
    public Task<ImagemProdutoResultado> Enviar(IFormFile foto)
    {
        Envios++;
        if (Recusar) throw new RegraProdutoException("Envie uma foto JPG, PNG ou WebP.");
        return Task.FromResult(new ImagemProdutoResultado { Url = "https://example.com/produto.png", PublicId = "kiora/produtos/ficticio" });
    }
    public Task<bool> Excluir(string publicId) => throw new NotSupportedException();
}

internal sealed class FalhaGravacaoMobile : SaveChangesInterceptor
{
    public bool Falhar { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Falhar) throw new DbUpdateException("Falha simulada, sem dados externos.");
        return ValueTask.FromResult(result);
    }
}
