using KioraRestaurante.Data;
using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Models;
using KioraRestaurante.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class CarrinhoConcorrenciaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbaAntigaNaoSobrescreveQuantidadeNemRemoveOuEsvazia(bool visitante)
    {
        using var banco = new BancoCarrinho(visitante);
        using var primeira = banco.Contexto();
        var service = new CarrinhoService(primeira);
        var antes = await service.BuscarCarrinho(banco.Acesso);
        var depois = await service.AtualizarQuantidade(banco.Acesso, banco.ProdutoId,
            new() { Quantidade = 5, VersaoEsperada = antes.Versao });
        Assert.NotEqual(antes.Versao, depois.Versao);
        using var segunda = banco.Contexto();
        var antiga = new CarrinhoService(segunda);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => antiga.AtualizarQuantidade(
            banco.Acesso, banco.ProdutoId, new() { Quantidade = 2, VersaoEsperada = antes.Versao }));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => antiga.RemoverProduto(banco.Acesso, banco.ProdutoId, antes.Versao));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => antiga.EsvaziarCarrinho(banco.Acesso, antes.Versao));
        using var consulta = banco.Contexto();
        Assert.Equal(5, Assert.Single((await new CarrinhoService(consulta).BuscarCarrinho(banco.Acesso)).Itens).Quantidade);
        Assert.False(segunda.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task AlteracaoConcorrenteDepoisDaLeituraTambemFazRollback()
    {
        using var banco = new BancoCarrinho(false);
        using var primeira = banco.Contexto();
        using var segunda = banco.Contexto();
        var a = new CarrinhoService(primeira);
        var b = new CarrinhoService(segunda);
        var versao = (await a.BuscarCarrinho(banco.Acesso)).Versao;
        await b.BuscarCarrinho(banco.Acesso); // Ambas leram a mesma versão antes de qualquer gravação.
        await a.AtualizarQuantidade(banco.Acesso, banco.ProdutoId, new() { Quantidade = 5, VersaoEsperada = versao });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.AtualizarQuantidade(
            banco.Acesso, banco.ProdutoId, new() { Quantidade = 2, VersaoEsperada = versao }));
        using var consulta = banco.Contexto();
        Assert.Equal(5, await consulta.ItensCarrinho.Select(i => i.Quantidade).SingleAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task VersaoAusenteOuVaziaNaoAlteraDados(string? valor)
    {
        using var banco = new BancoCarrinho(false);
        using var contexto = banco.Contexto();
        var service = new CarrinhoService(contexto);
        Guid? versao = valor is null ? null : Guid.Parse(valor);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AtualizarQuantidade(
            banco.Acesso, banco.ProdutoId, new() { Quantidade = 2, VersaoEsperada = versao }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RemoverProduto(banco.Acesso, banco.ProdutoId, versao));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EsvaziarCarrinho(banco.Acesso, versao));
        Assert.False(contexto.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VersaoAtualPermiteRemoverOuEsvaziar(bool esvaziar)
    {
        using var banco = new BancoCarrinho(false);
        using var contexto = banco.Contexto();
        var service = new CarrinhoService(contexto);
        var antes = await service.BuscarCarrinho(banco.Acesso);
        var depois = esvaziar ? await service.EsvaziarCarrinho(banco.Acesso, antes.Versao)
            : await service.RemoverProduto(banco.Acesso, banco.ProdutoId, antes.Versao);
        Assert.Empty(depois.Itens);
        Assert.NotEqual(antes.Versao, depois.Versao);
    }

    private sealed class BancoCarrinho : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly DbContextOptions<AppDbContext> _opcoes;
        public AcessoCarrinho Acesso { get; }
        public int ProdutoId { get; }
        public BancoCarrinho(bool visitante)
        {
            _conexao.Open();
            _opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options;
            using var contexto = Contexto();
            contexto.Database.EnsureCreated();
            var usuario = new Usuario { Nome = "Cliente", Email = "cliente@teste.local", SenhaHash = "teste" };
            var produto = new Produto { Nome = "Teste", Descricao = "Teste", CategoriaId = 1, Preco = 10m };
            contexto.AddRange(usuario, produto);
            contexto.SaveChanges();
            ProdutoId = produto.Id;
            var carrinho = new Carrinho
            {
                UsuarioId = visitante ? null : usuario.Id,
                ExpiraEmUtc = visitante ? DateTime.UtcNow.AddDays(1) : null,
                ItensCarrinho = [new() { ProdutoId = produto.Id, Quantidade = 1 }]
            };
            contexto.Add(carrinho);
            contexto.SaveChanges();
            Acesso = new() { UsuarioId = visitante ? null : usuario.Id, CarrinhoVisitanteId = visitante ? carrinho.Id : null };
        }
        public AppDbContext Contexto() => new(_opcoes);
        public void Dispose() => _conexao.Dispose();
    }
}
