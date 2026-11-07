using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Endereco;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Entrega;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;
using Opcoes = Microsoft.Extensions.Options.Options;

namespace KioraRestaurante.Tests;

public class CheckoutEntregaTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5000, 5)]
    [InlineData(5000.01, 8)]
    [InlineData(10000, 8)]
    [InlineData(10000.01, 12)]
    [InlineData(15000, 12)]
    [InlineData(15000.01, 16)]
    [InlineData(20000, 16)]
    [InlineData(20000.01, 20)]
    [InlineData(25000, 20)]
    [InlineData(25000.01, 24)]
    [InlineData(30000, 24)]
    [InlineData(30000.01, 28)]
    [InlineData(35000, 28)]
    public void TaxasRespeitamLimitesInclusivos(decimal metros, decimal esperado)
    {
        using var banco = new Banco();
        Assert.Equal(esperado, banco.Entrega.CalcularTaxa(metros));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(35000.01)]
    public void NaoAceitaDistanciaInvalidaOuSuperiorAVinteKm(decimal metros)
    {
        using var banco = new Banco();
        Assert.Throws<ValidationException>(() => banco.Entrega.CalcularTaxa(metros));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClienteEscolheSeEnderecoSeraSalvoSemAlterarCopiaDoPedido(bool salvar)
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(salvar);
        var id = await banco.Pedidos.ConfirmarPedido(banco.Acesso, dto);
        var repetido = await banco.Pedidos.ConfirmarPedido(banco.Acesso, dto);
        banco.Db.ChangeTracker.Clear();
        var pedido = await banco.Pedidos.BuscarPedido(banco.Cliente.Id, id);
        Assert.Equal(id, repetido);
        Assert.Equal(45m, pedido!.ValorTotal);
        Assert.Equal(5m, pedido.TaxaEntrega);
        Assert.Equal(1000, pedido.DistanciaEntregaMetros);
        Assert.Equal(30, pedido.PreparoMinutos);
        Assert.Equal(600, pedido.DuracaoPercursoSegundos);
        Assert.Equal(40, pedido.PrazoTotalMinutos);
        Assert.Equal("Rua de Teste", pedido.EnderecoEntrega.Logradouro);
        Assert.Equal(salvar ? 1 : 0, await banco.Db.EnderecosUsuario.CountAsync());
        Assert.Empty(await banco.Db.ItensCarrinho.ToListAsync());

        if (salvar)
        {
            var endereco = await banco.Db.EnderecosUsuario.SingleAsync();
            await banco.Enderecos.Excluir(banco.Cliente.Id, endereco.Id, endereco.Versao);
            banco.Db.ChangeTracker.Clear();
            pedido = await banco.Pedidos.BuscarPedido(banco.Cliente.Id, id);
            Assert.Empty(await banco.Db.EnderecosUsuario.ToListAsync());
            Assert.Equal("77", pedido!.EnderecoEntrega.Numero);
        }
    }

    [Fact]
    public async Task ObservacaoERegistradaNoItemCertoSemMudarPreco()
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(false);
        var produto = await banco.Db.Produtos.SingleAsync();
        dto.Observacoes[produto.Id] = "  Sem cebola e pouco molho <script>não executar</script>  ";
        var id = await banco.Pedidos.ConfirmarPedido(banco.Acesso, dto);
        var pedido = await banco.Pedidos.BuscarPedido(banco.Cliente.Id, id);
        Assert.Equal("Sem cebola e pouco molho <script>não executar</script>", pedido!.ItensPedido.Single().Observacao);
        Assert.Equal(45m, pedido.ValorTotal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ObservacaoLongaOuDeOutroProdutoNaoConfirmaPedido(bool longa)
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(false);
        var produto = await banco.Db.Produtos.SingleAsync();
        dto.Observacoes[longa ? produto.Id : produto.Id + 99] = longa ? new string('x', 501) : "Sem molho";
        await Assert.ThrowsAsync<ValidationException>(() => banco.Pedidos.ConfirmarPedido(banco.Acesso, dto));
        Assert.Empty(await banco.Db.Pedidos.ToListAsync());
        Assert.Single(await banco.Db.ItensCarrinho.ToListAsync());
    }

    [Fact]
    public async Task FalhaNoPedidoNaoSalvaEnderecoNemEsvaziaCarrinho()
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(true);
        await banco.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_pedido BEFORE INSERT ON Pedidos BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => banco.Pedidos.ConfirmarPedido(banco.Acesso, dto));
        Assert.Empty(await banco.Db.EnderecosUsuario.ToListAsync());
        Assert.Empty(await banco.Db.Pedidos.ToListAsync());
        Assert.Single(await banco.Db.ItensCarrinho.ToListAsync());
    }

    [Fact]
    public async Task EnderecoAlteradoDepoisDaCotacaoExigeNovoCalculo()
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(true);
        dto.NovoEndereco!.Numero = "78";
        await Assert.ThrowsAsync<ValidationException>(() => banco.Pedidos.ConfirmarPedido(banco.Acesso, dto));
        Assert.Empty(await banco.Db.EnderecosUsuario.ToListAsync());
        Assert.Empty(await banco.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CotacaoNaoPodeSerReutilizadaPorOutroUsuarioOuAposMudarTabela()
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(false);
        var endereco = (await banco.Enderecos.PrepararParaCheckout(banco.Cliente.Id, dto)).Entrega;
        Assert.Throws<ValidationException>(() => banco.Entrega.Conferir(banco.Cliente.Id + 1, endereco, dto.TokenEntrega));
        banco.Opcoes.Faixas[0].Valor = 9m;
        Assert.Throws<ValidationException>(() => banco.Entrega.Conferir(banco.Cliente.Id, endereco, dto.TokenEntrega));
    }

    [Fact]
    public async Task CotacaoExpiradaOuAdulteradaNaoConfirmaPedido()
    {
        using var banco = new Banco();
        var dto = await banco.Preparar(false);
        var endereco = (await banco.Enderecos.PrepararParaCheckout(banco.Cliente.Id, dto)).Entrega;
        Assert.Throws<ValidationException>(() => banco.Entrega.Conferir(banco.Cliente.Id, endereco, dto.TokenEntrega + "alterado"));
        banco.Relogio.Agora = banco.Relogio.Agora.AddMinutes(16);
        await Assert.ThrowsAsync<ValidationException>(() => banco.Pedidos.ConfirmarPedido(banco.Acesso, dto));
    }

    [Fact]
    public async Task EnderecoSalvoTambemPrecisaDeCotacaoDentroDoLimite()
    {
        using var banco = new Banco();
        await banco.Enderecos.Salvar(banco.Cliente.Id, null, new()
        {
            Apelido = "Casa", Cep = "05422000", Logradouro = "Rua de Teste", Bairro = "Pinheiros", Numero = "77"
        });
        var salvo = await banco.Db.EnderecosUsuario.SingleAsync();
        var escolha = new CotarEntregaRequestDTO { EnderecoId = salvo.Id, EnderecoVersao = salvo.Versao };
        var endereco = (await banco.Enderecos.PrepararParaCheckout(banco.Cliente.Id, escolha)).Entrega;
        banco.Rotas.Metros = 35001;
        await Assert.ThrowsAsync<ValidationException>(() => banco.Entrega.Cotar(banco.Cliente.Id, endereco));
    }

    [Fact]
    public async Task NaoExcluiEnderecoDeOutroClienteNemVersaoAntiga()
    {
        using var banco = new Banco();
        await banco.Enderecos.Salvar(banco.Cliente.Id, null, new()
        {
            Apelido = "Casa", Cep = "05422000", Logradouro = "Rua de Teste", Bairro = "Pinheiros", Numero = "77"
        });
        var salvo = await banco.Db.EnderecosUsuario.SingleAsync();
        var outro = new Usuario { Nome = "Outro", Email = "outro@example.com", SenhaHash = "teste" };
        banco.Db.Add(outro); await banco.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => banco.Enderecos.Excluir(outro.Id, salvo.Id, salvo.Versao));
        await Assert.ThrowsAsync<ValidationException>(() => banco.Enderecos.Excluir(banco.Cliente.Id, salvo.Id, Guid.NewGuid()));
        Assert.Single(await banco.Db.EnderecosUsuario.ToListAsync());
    }

    [Fact]
    public void ValidacaoSoExigeCamposDoEnderecoSelecionado()
    {
        var salvo = new CotarEntregaRequestDTO { EnderecoId = 1, EnderecoVersao = Guid.NewGuid() };
        Validator.ValidateObject(salvo, new(salvo), true);
        var novo = new CotarEntregaRequestDTO();
        Assert.Throws<ValidationException>(() => Validator.ValidateObject(novo, new(novo), true));
    }

    private sealed class Banco : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly HttpClient _http = new(new CepFalso()) { BaseAddress = new("https://viacep.com.br/ws/") };
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public AppDbContext Db { get; }
        public Usuario Cliente { get; }
        public AcessoCarrinho Acesso => new() { UsuarioId = Cliente.Id };
        public PedidoService Pedidos { get; }
        public EnderecoUsuarioService Enderecos { get; }
        public EntregaOptions Opcoes { get; } = new();
        public RotasEntregaSimuladas Rotas { get; } = new();
        public RelogioFalso Relogio { get; } = new();
        public CotacaoEntregaService Entrega { get; }

        public Banco()
        {
            _conexao.Open();
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
            Db.Database.EnsureCreated();
            Cliente = new() { Nome = "Cliente", Email = "cliente@example.com", SenhaHash = "teste" };
            var produto = new Produto { Nome = "Produto", Descricao = "Teste", CategoriaId = 2, Preco = 20m };
            var carrinho = new Carrinho { Usuario = Cliente, ItensCarrinho = [new() { Produto = produto, Quantidade = 2 }] };
            Db.Add(carrinho); Db.SaveChanges();
            Enderecos = new(Db, new CepService(_http, _cache, new ConfigurationBuilder().Build()));
            var protecao = new EphemeralDataProtectionProvider();
            Entrega = new(Rotas, OpcoesCreate(), protecao, Relogio);
            Pedidos = new(Db, new CarrinhoService(Db), protecao, Enderecos, Entrega);
        }

        private Microsoft.Extensions.Options.IOptions<EntregaOptions> OpcoesCreate() => Microsoft.Extensions.Options.Options.Create(Opcoes);

        public async Task<CriarPedidoRequestDTO> Preparar(bool salvar)
        {
            var revisao = await Pedidos.PrepararCheckout(Acesso);
            var dto = new CriarPedidoRequestDTO
            {
                TokenRevisao = revisao.TokenRevisao, FormaPagamento = FormaPagamento.Dinheiro,
                NovoEndereco = new EnderecoCheckoutRequestDTO
                {
                    Cep = "05422000", Logradouro = "Rua informada", Numero = "77", Bairro = "Pinheiros",
                    SalvarNaConta = salvar, Apelido = salvar ? "Casa" : null
                }
            };
            var endereco = (await Enderecos.PrepararParaCheckout(Cliente.Id, dto)).Entrega;
            dto.TokenEntrega = (await Entrega.Cotar(Cliente.Id, endereco)).Token;
            return dto;
        }

        public void Dispose() { Db.Dispose(); _conexao.Dispose(); _http.Dispose(); _cache.Dispose(); }
    }

    private sealed class CepFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"logradouro":"Rua de Teste","bairro":"Pinheiros","localidade":"São Paulo","uf":"SP","ibge":"3550308"}""", Encoding.UTF8, "application/json")
            });
    }

    public sealed class RelogioFalso : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}

internal sealed class RotasEntregaSimuladas : IRotasEntrega
{
    public decimal Metros { get; set; } = 1000;
    public Task<RotaEntrega> Calcular(EnderecoEntrega destino, CancellationToken ct = default) => Task.FromResult(new RotaEntrega(Metros, 600));
}
