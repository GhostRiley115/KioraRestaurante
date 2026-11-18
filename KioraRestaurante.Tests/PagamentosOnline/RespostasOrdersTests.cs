using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KioraRestaurante.Controllers.Integracoes;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services.PagamentosOnline;
using KioraRestaurante.Services.Pedidos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using Xunit;

namespace KioraRestaurante.Tests;

public class RespostasOrdersTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("\"texto\"")]
    [InlineData("JSON inválido")]
    public async Task CriacaoComRespostaInvalidaPreservaPedidoPendenteERetentativa(string resposta)
    {
        using var b = new BancoOrders(registrado: false);
        b.Provedor.Resposta = resposta;

        await Assert.ThrowsAsync<FalhaPagamento>(() => b.Pagamentos.Iniciar(b.Cliente.Id, b.Pedido.Id, default));

        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Pendente, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
        var salvo = await b.Db.PagamentosOnline.SingleAsync();
        Assert.Null(salvo.OrderId);
        Assert.NotEmpty(salvo.CorpoCriacao);
        Assert.Equal(1, b.Provedor.Chamadas);
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"amanhã\"")]
    [InlineData("\"2026-99-99T99:00:00Z\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task DataExternaInvalidaNaoAprovaPagamentoNemAlteraConferencia(string campo)
    {
        using var b = new BancoOrders();
        var versao = b.Registro!.Versao;
        var resposta = b.RespostaValida();
        if (campo == "ausente") resposta.Remove("last_updated_date");
        else resposta["last_updated_date"] = JsonNode.Parse(campo);
        b.Provedor.Resposta = resposta.ToJsonString();

        await Assert.ThrowsAsync<FalhaPagamento>(() => b.Pagamentos.Sincronizar(BancoOrders.OrderId, default));

        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Pendente, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
        var salvo = await b.Db.PagamentosOnline.SingleAsync();
        Assert.Equal(versao, salvo.Versao);
        Assert.Null(salvo.AtualizacaoExternaUtc);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("-1")]
    [InlineData("\"-1\"")]
    [InlineData("\"NaN\"")]
    [InlineData("1e1000")]
    public async Task ValorPagoInvalidoNaoAprovaPedido(string campo)
    {
        using var b = new BancoOrders();
        var resposta = b.RespostaValida();
        resposta["total_paid_amount"] = JsonNode.Parse(campo);
        b.Provedor.Resposta = resposta.ToJsonString();

        await Assert.ThrowsAsync<FalhaPagamento>(() => b.Pagamentos.Sincronizar(BancoOrders.OrderId, default));

        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Pendente, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
        Assert.Null((await b.Db.PagamentosOnline.SingleAsync()).AtualizacaoExternaUtc);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void ValorDeTipoInvalidoNaoPodeSerInterpretadoComoCancelamentoSemPagamento(string campo)
    {
        using var json = JsonDocument.Parse("{\"status\":\"canceled\",\"total_paid_amount\":" + campo + "}");
        Assert.Throws<FalhaPagamento>(() => DadosOrder.EncerradaSemPagamento(json.RootElement));
    }

    [Theory]
    [InlineData("null", "created", "created", StatusPagamento.Pendente)]
    [InlineData("\"\"", "created", "created", StatusPagamento.Pendente)]
    [InlineData("null", "processed", "accredited", StatusPagamento.Conferencia)]
    [InlineData("\"\"", "processed", "accredited", StatusPagamento.Conferencia)]
    [InlineData("\"10.00\"", "processed", "accredited", StatusPagamento.Aprovado)]
    [InlineData("10", "processed", "accredited", StatusPagamento.Aprovado)]
    public async Task ValoresLegitimosMantemRegrasDeAprovacao(string campo, string status, string detalhe, StatusPagamento esperado)
    {
        using var b = new BancoOrders();
        var resposta = b.RespostaValida();
        resposta["total_paid_amount"] = JsonNode.Parse(campo);
        resposta["status"] = status;
        resposta["status_detail"] = detalhe;
        b.Provedor.Resposta = resposta.ToJsonString();

        await b.Pagamentos.Sincronizar(BancoOrders.OrderId, default);

        b.Db.ChangeTracker.Clear();
        Assert.Equal(esperado, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
    }

    [Fact]
    public async Task RespostaAntigaNaoRegridePagamentoJaConferido()
    {
        using var b = new BancoOrders();
        await b.Pagamentos.Sincronizar(BancoOrders.OrderId, default);
        var resposta = b.RespostaValida();
        resposta["last_updated_date"] = "2026-10-01T12:00:00Z";
        resposta["status"] = "created";
        resposta["status_detail"] = "created";
        resposta["total_paid_amount"] = null;
        b.Provedor.Resposta = resposta.ToJsonString();

        await b.Pagamentos.Sincronizar(BancoOrders.OrderId, default);

        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Aprovado, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
    }

    [Theory]
    [InlineData("canceled", null, StatusPagamento.Cancelado)]
    [InlineData("canceled", "0", StatusPagamento.Cancelado)]
    [InlineData("canceled", "0.01", StatusPagamento.Conferencia)]
    [InlineData("canceled", "10", StatusPagamento.Conferencia)]
    [InlineData("failed", null, StatusPagamento.Recusado)]
    [InlineData("failed", "0", StatusPagamento.Recusado)]
    [InlineData("failed", "0.01", StatusPagamento.Conferencia)]
    [InlineData("failed", "10", StatusPagamento.Conferencia)]
    public void CancelamentoOuRecusaComValorPagoExigeConferencia(string status, string? pago, StatusPagamento esperado)
    {
        var valor = pago == null ? (decimal?)null : decimal.Parse(pago, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(esperado, CheckoutOnlineService.Mapear(status, status, valor, 10m));
    }

    [Theory]
    [InlineData("canceled", "1.00")]
    [InlineData("canceled", "10.00")]
    [InlineData("failed", "1.00")]
    [InlineData("failed", "10.00")]
    public async Task SincronizacaoNaoEncerraPagamentoComDinheiroRecebidoEmEstadoInconsistente(string status, string pago)
    {
        using var b = new BancoOrders();
        var resposta = b.RespostaValida();
        resposta["status"] = status;
        resposta["status_detail"] = status;
        resposta["total_paid_amount"] = pago;
        b.Provedor.Resposta = resposta.ToJsonString();

        await b.Pagamentos.Sincronizar(BancoOrders.OrderId, default);

        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Conferencia, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
        var registro = await b.Db.PagamentosOnline.SingleAsync();
        Assert.True(registro.RevisaoNecessaria);
        Assert.Equal(status + "/" + status, registro.SituacaoExterna);
        Assert.NotNull(registro.AtualizacaoExternaUtc);
    }
}

// Somente SQLite em memória e transporte simulado; nenhuma integração externa é acessada.
internal sealed class BancoOrders : IDisposable
{
    public const string OrderId = "ORDTST123";
    public const string Segredo = "segredo-ficticio-exclusivo-dos-testes";
    public const string RequestId = "requisicao-ficticia";
    public const string Assinatura = "ts=1700000000,v1=ed7935364e61ef69975f4ea0709e07671cbc216fda02cefad88336ae78031713";
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly HttpClient _http;
    public AppDbContext Db { get; }
    public Usuario Cliente { get; }
    public Pedido Pedido { get; }
    public PagamentoOnline? Registro { get; }
    public MercadoPagoConfig Config { get; } = new()
    {
        Ativado = true, ModoTeste = true, AccessToken = "credencial-ficticia",
        VendedorId = "123", ApplicationId = "456", UrlPublica = "https://kiora.example.com", WebhookSecret = Segredo
    };
    public TransporteOrders Provedor { get; } = new();
    public CheckoutOnlineService Pagamentos { get; }

    public BancoOrders(bool registrado = true)
    {
        _conexao.Open();
        Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
        Db.Database.EnsureCreated();
        Cliente = new() { Nome = "Cliente fictício", Email = "cliente@example.org", SenhaHash = "não-utilizada" };
        Pedido = new()
        {
            Usuario = Cliente, FormaPagamento = FormaPagamento.MercadoPago, ValorTotal = 10m,
            ItensPedido = [new()
            {
                Produto = new() { Nome = "Produto", Descricao = "Teste", CategoriaId = 2, Preco = 10m },
                NomeProduto = "Produto", Quantidade = 1, PrecoUnitario = 10m
            }]
        };
        Db.Add(Pedido);
        if (registrado)
        {
            Registro = new() { Pedido = Pedido, OrderId = OrderId, Valor = 10m };
            Db.Add(Registro);
        }
        Db.SaveChanges();
        _http = new(Provedor) { BaseAddress = new("https://api.mercadopago.com/") };
        Pagamentos = new(Db, new(_http, OptionsFactory.Create(Config)), OptionsFactory.Create(Config));
        Provedor.Resposta = RespostaValida().ToJsonString();
    }

    public JsonObject RespostaValida() => new()
    {
        ["id"] = OrderId, ["type"] = "online", ["processing_mode"] = "manual",
        ["external_reference"] = Registro?.Referencia.ToString("N") ?? Guid.NewGuid().ToString("N"),
        ["user_id"] = "123", ["currency"] = "BRL", ["country_code"] = "BRA", ["total_amount"] = "10.00",
        ["integration_data"] = new JsonObject { ["application_id"] = "456" },
        ["status"] = "processed", ["status_detail"] = "accredited", ["total_paid_amount"] = "10.00",
        ["last_updated_date"] = "2026-11-17T12:00:00-03:00"
    };

    public MpOrdersWebhookController Webhook()
    {
        var api = new MpOrdersClient(_http, OptionsFactory.Create(Config));
        var operacoes = new GerenciarPedidoService(Db, null!, null!, api, Pagamentos, OptionsFactory.Create(Config),
            TimeProvider.System, NullLogger<GerenciarPedidoService>.Instance);
        var controller = new MpOrdersWebhookController(Pagamentos, OptionsFactory.Create(Config),
            NullLogger<MpOrdersWebhookController>.Instance, operacoes)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.Request.QueryString = new("?type=order&data.id=" + OrderId);
        controller.Request.Headers["x-signature"] = Assinatura;
        controller.Request.Headers["x-request-id"] = RequestId;
        return controller;
    }

    public void Dispose() { _http.Dispose(); Db.Dispose(); _conexao.Dispose(); }
}

internal sealed class TransporteOrders : HttpMessageHandler
{
    public string Resposta { get; set; } = "{}";
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public int Chamadas { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Chamadas++;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Resposta, Encoding.UTF8, "application/json") });
    }
}
