using KioraRestaurante.Controllers.Api;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Cardapio;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using KioraRestaurante.Services.Produtos;
using KioraRestaurante.ViewModels.Cardapio;

namespace KioraRestaurante.Tests;

public class CardapioMobileTests
{
    [Fact]
    public async Task BuscaSemAcentoEValoresComVirgulaUsamPrecoFinalComDesconto()
    {
        using var b = new CatalogoMobile();
        var lista = await b.Listar(new() { Busca = "LAMEN", PrecoMinimo = "30,00", PrecoMaximo = "38.00" });
        var produto = Assert.Single(lista);
        Assert.Equal(b.Banco.Produto.Id, produto.Id);
        Assert.Equal(36m, produto.Preco);
        Assert.Equal(40m, produto.PrecoOriginal);
        Assert.Equal(10m, produto.DescontoPercentual);
    }

    [Theory]
    [InlineData("menor-preco", "Água")]
    [InlineData("maior-preco", "Combo Kiora")]
    [InlineData("nome-az", "Água")]
    [InlineData("nome-za", "Lámen da casa")]
    public async Task OrdenacoesPublicasUsamOsNomesDoContratoWeb(string ordenacao, string primeiro)
    {
        using var b = new CatalogoMobile();
        var produtos = await b.Listar(new() { Ordenacao = ordenacao });
        Assert.Equal(primeiro, produtos.First().Nome);
        Assert.Equal(4, produtos.Count);
    }

    [Fact]
    public async Task PadraoSeparaCombosDosPratosEOrdenaCategoriasComoNoSite()
    {
        using var b = new CatalogoMobile();
        var produtos = await b.Listar(new());
        Assert.Equal(new[] { b.Combo.Id, b.Banco.Produto.Id, b.Entrada.Id, b.Bebida.Id }, produtos.Select(p => p.Id));
        var categorias = Assert.IsType<OkObjectResult>((await b.Api.ListarCategorias()).Result).Value;
        Assert.Equal(new[] { 2, 1, 4, 3 }, Assert.IsType<List<KioraRestaurante.DTOs.Categoria.CategoriaResponseDTO>>(categorias).Select(c => c.Id));
    }

    [Fact]
    public async Task ColecaoCombosTrazComposicaoEPromocoesIncluemComboOuProdutoComDesconto()
    {
        using var b = new CatalogoMobile();
        var combo = Assert.Single(await b.Listar(new() { Colecao = "combos" }));
        Assert.True(combo.EhCombo);
        Assert.Contains("1 × Lámen da casa", combo.Composicao);
        Assert.Contains("2 × Guioza", combo.Composicao);
        var promocoes = await b.Listar(new() { Colecao = "promocoes" });
        Assert.Equal(new[] { b.Combo.Id, b.Banco.Produto.Id }.OrderBy(i => i), promocoes.Select(p => p.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task MaisPedidosContaApenasComprasValidasDaQuinzenaEResetaNaProxima()
    {
        using var b = new CatalogoMobile();
        b.Venda(b.Banco.Produto, 2);
        b.Venda(b.Entrada, 5);
        b.Venda(b.Combo, 30, data: b.Relogio.Agora.AddDays(-14));
        b.Venda(b.Bebida, 30, cancelado: true);
        b.Venda(b.Combo, 30, pago: false);
        b.Venda(b.Combo, 30, data: b.Relogio.Agora.AddHours(1));
        await b.Banco.Db.SaveChangesAsync();
        Assert.Equal(new[] { b.Entrada.Id, b.Banco.Produto.Id }, (await b.Listar(new() { Colecao = "mais-pedidos" })).Select(p => p.Id));
        b.Relogio.Agora = b.Relogio.Agora.AddDays(14);
        Assert.Empty(await b.Listar(new() { Colecao = "mais-pedidos" }));
    }

    [Theory]
    [InlineData("preco-invertido")]
    [InlineData("preco-invalido")]
    [InlineData("colecao")]
    [InlineData("ordenacao")]
    [InlineData("categoria-colecao")]
    public async Task FiltroInvalidoRetorna400EmVezDeIgnorarCampo(string erro)
    {
        using var b = new CatalogoMobile();
        var filtro = new FiltroCardapioViewModel();
        if (erro == "preco-invertido") { filtro.PrecoMinimo = "50"; filtro.PrecoMaximo = "20"; }
        if (erro == "preco-invalido") filtro.PrecoMinimo = "1.000,00";
        if (erro == "colecao") filtro.Colecao = "inexistente";
        if (erro == "ordenacao") filtro.Ordenacao = "errada";
        if (erro == "categoria-colecao") filtro.Colecao = "combos";
        Assert.IsType<BadRequestObjectResult>((await b.Api.ListarProdutos(erro == "categoria-colecao" ? 2 : null, filtro)).Result);
    }

    [Fact]
    public async Task ConsultaPorIdNaoLeOutroProdutoNemExpoeInativosECategoriasDesativadas()
    {
        using var b = new CatalogoMobile();
        b.Banco.Db.Add(new Produto { Nome = "Legado inválido", Descricao = "Teste", Preco = 20, DescontoPercentual = 91, CategoriaId = 2 });
        await b.Banco.Db.SaveChangesAsync();
        var dto = Assert.IsType<ProdutoResponseDTO>(Assert.IsType<OkObjectResult>((await b.Api.BuscarProduto(b.Combo.Id)).Result).Value);
        Assert.Equal(b.Combo.Id, dto.Id);
        Assert.NotNull(dto.Composicao);
        Assert.IsType<NotFoundObjectResult>((await b.Api.BuscarProduto(b.Oculto.Id)).Result);
        Assert.IsType<NotFoundObjectResult>((await b.Api.BuscarProduto(b.CategoriaInativa.Id)).Result);
        Assert.IsType<NotFoundObjectResult>((await b.Api.BuscarProduto(999)).Result);
    }

    [Fact]
    public async Task FiltroDeDisponibilidadeRespeitaComponentesDoCombo()
    {
        using var b = new CatalogoMobile();
        b.Entrada.Disponivel = false;
        await b.Banco.Db.SaveChangesAsync();
        var combo = Assert.Single(await b.Listar(new() { Colecao = "combos" }));
        Assert.False(combo.Disponivel);
        Assert.Empty(await b.Listar(new() { Colecao = "combos", SomenteDisponiveis = true }));
        Assert.Equal(new[] { b.Bebida.Id, b.Banco.Produto.Id }.OrderBy(i => i), (await b.Listar(new() { SomenteDisponiveis = true })).Select(p => p.Id).OrderBy(i => i));
    }

    private sealed class CatalogoMobile : IDisposable
    {
        public BancoProdutosMobile Banco { get; } = new();
        public RelogioCatalogoMobile Relogio { get; } = new();
        public Produto Entrada { get; }
        public Produto Bebida { get; }
        public Produto Combo { get; }
        public Produto Oculto { get; }
        public Produto CategoriaInativa { get; }
        public CardapioApiController Api { get; }
        public CatalogoMobile()
        {
            Entrada = new() { Nome = "Guioza", Descricao = "Entrada", Preco = 15.20m, CategoriaId = 1 };
            Bebida = new() { Nome = "Água", Descricao = "Bebida", Preco = 6.50m, CategoriaId = 3 };
            Combo = new() { Nome = "Combo Kiora", Descricao = "Combo", Preco = 50m, CategoriaId = 2, EhCombo = true,
                Componentes = [new() { Produto = Banco.Produto, Quantidade = 1 }, new() { Produto = Entrada, Quantidade = 2 }] };
            Oculto = new() { Nome = "Oculto", Descricao = "Inativo", Preco = 10, CategoriaId = 1, Ativo = false };
            var categoria = new Categoria { Nome = "Antiga", Ativa = false };
            CategoriaInativa = new() { Nome = "Fora do catálogo", Descricao = "Categoria inativa", Preco = 10, Categoria = categoria };
            Banco.Db.AddRange(Entrada, Bebida, Combo, Oculto, CategoriaInativa);
            Banco.Db.SaveChanges();
            Api = new(new CategoriaService(Banco.Db), Banco.Produtos, new MaisPedidosService(Banco.Db, Relogio));
        }
        public async Task<List<ProdutoResponseDTO>> Listar(FiltroCardapioViewModel filtro) =>
            Assert.IsType<List<ProdutoResponseDTO>>(Assert.IsType<OkObjectResult>((await Api.ListarProdutos(null, filtro)).Result).Value);
        public void Venda(Produto produto, int quantidade, bool cancelado = false, bool pago = true, DateTimeOffset? data = null) =>
            Banco.Db.Add(new Pedido { UsuarioId = Banco.Administrador.Id, DataPedido = (data ?? Relogio.Agora).UtcDateTime,
                StatusPagamento = pago ? StatusPagamento.Aprovado : StatusPagamento.Pendente,
                StatusPedido = cancelado ? StatusPedido.Cancelado : StatusPedido.Entregue,
                ItensPedido = [new() { ProdutoId = produto.Id, NomeProduto = produto.Nome, Quantidade = quantidade, PrecoUnitario = produto.Preco }] });
        public void Dispose() => Banco.Dispose();
    }
    private sealed class RelogioCatalogoMobile : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
