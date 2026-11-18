using System.Net;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class WebhookOrdersTests
{
    [Fact]
    public void VetorConhecidoValidaAssinaturaComIdNormalizadoEPermiteReenvio()
    {
        Assert.True(AssinaturaOrders.Validar(BancoOrders.Assinatura, BancoOrders.RequestId, BancoOrders.OrderId, BancoOrders.Segredo));
        Assert.True(AssinaturaOrders.Validar(BancoOrders.Assinatura, BancoOrders.RequestId, "ordtst123", BancoOrders.Segredo));
    }

    [Theory]
    [InlineData("sem-assinatura")]
    [InlineData("sem-requisicao")]
    [InlineData("requisicao-alterada")]
    [InlineData("segredo-alterado")]
    [InlineData("digest-invalido")]
    [InlineData("ts-duplicado")]
    [InlineData("v1-duplicado")]
    [InlineData("id-alterado")]
    [InlineData("id-invalido")]
    [InlineData("id-duplicado")]
    [InlineData("requisicao-duplicada")]
    public async Task NotificacaoInvalidaNaoConsultaProvedorNemAlteraPagamento(string cenario)
    {
        using var b = new BancoOrders();
        var controller = b.Webhook();
        switch (cenario)
        {
            case "sem-assinatura": controller.Request.Headers.Remove("x-signature"); break;
            case "sem-requisicao": controller.Request.Headers.Remove("x-request-id"); break;
            case "requisicao-alterada": controller.Request.Headers["x-request-id"] = "outra"; break;
            case "segredo-alterado": b.Config.WebhookSecret = "outro-segredo"; break;
            case "digest-invalido": controller.Request.Headers["x-signature"] = "ts=1700000000,v1=ZZ"; break;
            case "ts-duplicado": controller.Request.Headers["x-signature"] = BancoOrders.Assinatura + ",ts=1700000000"; break;
            case "v1-duplicado": controller.Request.Headers["x-signature"] = BancoOrders.Assinatura + ",v1=00"; break;
            case "id-alterado": controller.Request.QueryString = new("?type=order&data.id=ORDTST999"); break;
            case "id-invalido": controller.Request.QueryString = new("?type=order&data.id=ORDTST%2F123"); break;
            case "id-duplicado": controller.Request.QueryString = new("?type=order&data.id=ORDTST123&data.id=ORDTST999"); break;
            case "requisicao-duplicada": controller.Request.Headers["x-request-id"] = new Microsoft.Extensions.Primitives.StringValues([BancoOrders.RequestId, "outra"]); break;
        }

        Assert.IsType<UnauthorizedResult>(await controller.Receber(default));
        Assert.Equal(0, b.Provedor.Chamadas);
        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Pendente, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
    }

    [Fact]
    public async Task ReenvioAssinadoConsultaEstadoAtualSemDuplicarAvisoDePagamento()
    {
        using var b = new BancoOrders();
        var controller = b.Webhook();
        Assert.IsType<OkResult>(await controller.Receber(default));
        var eventos = await b.Db.EventosNotificacao.CountAsync();

        Assert.IsType<OkResult>(await controller.Receber(default));

        b.Db.ChangeTracker.Clear();
        Assert.Equal(2, b.Provedor.Chamadas);
        Assert.Equal(StatusPagamento.Aprovado, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
        Assert.Equal(eventos, await b.Db.EventosNotificacao.CountAsync());
    }

    [Theory]
    [InlineData("json-invalido")]
    [InlineData("estrutura-invalida")]
    [InlineData("data-invalida")]
    [InlineData("http-indisponivel")]
    public async Task FalhaDoProvedorPermiteReenvioSemConfirmarPagamento(string cenario)
    {
        using var b = new BancoOrders();
        if (cenario == "json-invalido") b.Provedor.Resposta = "JSON inválido";
        if (cenario == "estrutura-invalida") b.Provedor.Resposta = "[]";
        if (cenario == "http-indisponivel") b.Provedor.Status = HttpStatusCode.ServiceUnavailable;
        if (cenario == "data-invalida")
        {
            var resposta = b.RespostaValida(); resposta["last_updated_date"] = "inválida";
            b.Provedor.Resposta = resposta.ToJsonString();
        }

        var resultado = Assert.IsType<StatusCodeResult>(await b.Webhook().Receber(default));

        Assert.Equal(503, resultado.StatusCode);
        Assert.Equal(1, b.Provedor.Chamadas);
        b.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPagamento.Pendente, (await b.Db.Pedidos.SingleAsync()).StatusPagamento);
    }

    [Theory]
    [InlineData(false, "order", 503)]
    [InlineData(true, "payment", 400)]
    public async Task IntegracaoDesativadaOuOutroTipoNaoConsultaProvedor(bool ativado, string tipo, int status)
    {
        using var b = new BancoOrders();
        b.Config.Ativado = ativado;
        var controller = b.Webhook();
        controller.Request.QueryString = new("?type=" + tipo + "&data.id=" + BancoOrders.OrderId);

        var resultado = Assert.IsAssignableFrom<StatusCodeResult>(await controller.Receber(default));

        Assert.Equal(status, resultado.StatusCode);
        Assert.Equal(0, b.Provedor.Chamadas);
    }
}
