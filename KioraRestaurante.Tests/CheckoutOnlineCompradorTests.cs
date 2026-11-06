using System.Net;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Xunit;

namespace KioraRestaurante.Tests;

public class CheckoutOnlineCompradorTests
{
    [Theory]
    [InlineData(true, "comprador.kiora@example.com")]
    [InlineData(false, "cliente@example.org")]
    public async Task CriacaoIdentificaCompradorConformeAmbiente(bool teste, string esperado)
    {
        using var banco = new Banco(teste);
        await banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default);

        var enviada = Assert.Single(banco.Http.Criacoes);
        using var json = JsonDocument.Parse(enviada.Corpo);
        Assert.Equal(esperado, json.RootElement.GetProperty("payer").GetProperty("email").GetString());
        Assert.Equal("10.00", json.RootElement.GetProperty("total_amount").GetString());
        Assert.Equal("manual", json.RootElement.GetProperty("processing_mode").GetString());
        Assert.Equal("automatic_async", json.RootElement.GetProperty("capture_mode").GetString());
        var salvo = await banco.Db.PagamentosOnline.SingleAsync();
        Assert.Equal(enviada.Corpo, salvo.CorpoCriacao);
        Assert.NotNull(salvo.OrderId);
    }

    [Fact]
    public async Task EmailDeTestePodeSerConfigurado()
    {
        using var banco = new Banco(true);
        banco.Config.EmailCompradorTeste = "outro.comprador@example.com";
        await banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default);
        using var json = JsonDocument.Parse(Assert.Single(banco.Http.Criacoes).Corpo);
        Assert.Equal("outro.comprador@example.com", json.RootElement.GetProperty("payer").GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public async Task EmailDeTesteInvalidoNaoCriaCobranca(string email)
    {
        using var banco = new Banco(true);
        banco.Config.EmailCompradorTeste = email;
        await Assert.ThrowsAsync<FalhaPagamento>(() => banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default));
        Assert.Empty(banco.Http.Criacoes);
        Assert.Empty(await banco.Db.PagamentosOnline.ToListAsync());
    }

    [Fact]
    public async Task OutroUsuarioNaoPodeCriarCobrancaParaOPedido()
    {
        using var banco = new Banco(true);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => banco.Servico.Iniciar(banco.Cliente.Id + 100, banco.Pedido.Id, default));
        Assert.Empty(banco.Http.Criacoes);
    }

    [Fact]
    public async Task RetentativaPreservaCorpoEChaveMesmoAposMudancaDeConfiguracao()
    {
        using var banco = new Banco(true);
        banco.Http.FalharUmaVez = true;
        await Assert.ThrowsAsync<FalhaPagamento>(() => banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default));
        banco.Config.EmailCompradorTeste = "alterado@example.com";
        await banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default);

        Assert.Equal(2, banco.Http.Criacoes.Count);
        Assert.Equal(banco.Http.Criacoes[0], banco.Http.Criacoes[1]);
    }

    [Fact]
    public async Task ConferenciaSoAprovaAposConsultarValorIntegralNoProvedor()
    {
        using var banco = new Banco(true);
        await banco.Servico.Iniciar(banco.Cliente.Id, banco.Pedido.Id, default);
        banco.Http.Pago = true;
        var id = (await banco.Db.PagamentosOnline.SingleAsync()).OrderId!;
        await banco.Servico.Sincronizar(id, default);
        banco.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Aprovado, (await banco.Db.Pedidos.SingleAsync()).StatusPagamento);
    }

    private sealed class Banco : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly HttpClient _clienteHttp;
        public AppDbContext Db { get; }
        public Usuario Cliente { get; }
        public Pedido Pedido { get; }
        public MercadoPagoConfig Config { get; }
        public ProvedorFalso Http { get; }
        public CheckoutOnlineService Servico { get; }

        public Banco(bool teste)
        {
            _conexao.Open();
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
            Db.Database.EnsureCreated();
            Cliente = new() { Nome = "Cliente", Email = "cliente@example.org", SenhaHash = "nao-utilizada" };
            var produto = new Produto { Nome = "Produto", Descricao = "Teste", CategoriaId = 2, Preco = 10m };
            Pedido = new()
            {
                Usuario = Cliente, FormaPagamento = FormaPagamento.MercadoPago,
                StatusPagamento = StatusPagamento.Pendente, ValorTotal = 10m,
                ItensPedido = [new() { Produto = produto, NomeProduto = "Produto", Quantidade = 1, PrecoUnitario = 10m }]
            };
            Db.Add(Pedido);
            Db.SaveChanges();
            Config = new()
            {
                Ativado = true, ModoTeste = teste, AccessToken = "credencial-ficticia",
                ApplicationId = "456", VendedorId = "123", UrlPublica = "https://kiora.example.com"
            };
            Http = new(teste);
            _clienteHttp = new(Http) { BaseAddress = new("https://api.mercadopago.com/") };
            Servico = new(Db, new(_clienteHttp, OptionsFactory.Create(Config)), OptionsFactory.Create(Config));
        }

        public void Dispose()
        {
            _clienteHttp.Dispose();
            Db.Dispose();
            _conexao.Dispose();
        }
    }

    // Substitui o provedor externo: os testes não usam credenciais nem banco da nuvem.
    private sealed class ProvedorFalso(bool teste) : HttpMessageHandler
    {
        public List<(string Corpo, string Chave)> Criacoes { get; } = [];
        public bool FalharUmaVez { get; set; }
        public bool Pago { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                Criacoes.Add((await request.Content!.ReadAsStringAsync(ct), request.Headers.GetValues("X-Idempotency-Key").Single()));
                if (FalharUmaVez)
                {
                    FalharUmaVez = false;
                    throw new HttpRequestException("Timeout simulado");
                }
            }

            using var corpo = JsonDocument.Parse(Criacoes[0].Corpo);
            var resposta = JsonSerializer.Serialize(new
            {
                id = teste ? "ORDTST123" : "ORD123", type = "online", processing_mode = "manual",
                external_reference = corpo.RootElement.GetProperty("external_reference").GetString(),
                user_id = "123", currency = "BRL", country_code = "BRA", total_amount = "10.00",
                integration_data = new { application_id = "456" },
                checkout_url = "https://www.mercadopago.com.br/checkout/v1/redirect?order_id=" + (teste ? "ORDTST123" : "ORD123"),
                status = Pago ? "processed" : "created", status_detail = Pago ? "accredited" : "created",
                total_paid_amount = Pago ? "10.00" : null,
                last_updated_date = DateTimeOffset.UtcNow.ToString("O")
            });
            return new(HttpStatusCode.OK) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
        }
    }
}
