using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

/// <summary>
/// Testes HTTP dos relatórios financeiros
/// e gerenciais do Kiora Mobile.
///
/// Reutiliza os dados fictícios dos testes
/// do dashboard administrativo.
///
/// Não utiliza o banco de produção.
/// </summary>
public sealed class RelatoriosAdminApiHttpTests
{
    private const string Rota =
        "/api/admin/relatorios";

    private const string Periodo =
        "?inicio=2026-11-01&fim=2026-11-17";

    // ==================================================
    // MÉTODOS AUXILIARES
    // ==================================================

    private static void Autenticar(
        HttpClient http,
        HostMobile host,
        TipoUsuario cargo = TipoUsuario.Administrador)
    {
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                host.Token(cargo));
    }

    private static async Task<JsonElement> Consultar(
        HttpClient http,
        string filtros = Periodo)
    {
        using var resposta =
            await http.GetAsync(Rota + filtros);

        Assert.Equal(
            HttpStatusCode.OK,
            resposta.StatusCode);

        var json = await resposta.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.True(
            json.GetProperty("sucesso")
                .GetBoolean());

        return json;
    }

    private static decimal Decimal(
        JsonElement json,
        string propriedade)
    {
        return json
            .GetProperty(propriedade)
            .GetDecimal();
    }

    private static int Inteiro(
        JsonElement json,
        string propriedade)
    {
        return json
            .GetProperty(propriedade)
            .GetInt32();
    }

    // ==================================================
    // TESTE 1 — AUTENTICAÇÃO E PERMISSÕES
    // ==================================================

    [Fact]
    public async Task RelatoriosExigemAdministradorAutenticado()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        // Sem autenticação.
        using var anonimo =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonimo.StatusCode);

        // Token inválido.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-invalido");

        using var invalido =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            invalido.StatusCode);

        // Verifica todos os cargos existentes.
        foreach (
            var cargo in Enum.GetValues<TipoUsuario>())
        {
            Autenticar(
                http,
                host,
                cargo);

            using var resposta =
                await http.GetAsync(Rota);

            var esperado =
                cargo == TipoUsuario.Administrador
                    ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden;

            Assert.Equal(
                esperado,
                resposta.StatusCode);
        }
    }

    // ==================================================
    // TESTE 2 — FILTROS POR CANAL
    // ==================================================

    [Theory]
    [InlineData("todos", 100, 60, 40, 2, 3)]
    [InlineData("delivery", 60, 60, 0, 1, 2)]
    [InlineData("salao", 40, 0, 40, 1, 1)]
    public async Task RelatorioRespeitaCanalSelecionado(
        string canal,
        int totalEsperado,
        int deliveryEsperado,
        int salaoEsperado,
        int vendasEsperadas,
        int unidadesEsperadas)
    {
        await using var host =
            await HostMobile.Criar();

        await DashboardAdminApiHttpTests
            .InserirDadosDeTeste(host);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var json = await Consultar(
            http,
            Periodo + "&canal=" + canal);

        var resumo =
            json.GetProperty("resumo");

        var periodo =
            json.GetProperty("periodo");

        var delivery =
            json.GetProperty("delivery");

        var salao =
            json.GetProperty("salao");

        var graficos =
            json.GetProperty("graficos");

        var vendas =
            json.GetProperty("vendas");

        Assert.Equal(
            canal,
            periodo.GetProperty("canal")
                .GetString());

        Assert.Equal(
            (decimal)totalEsperado,
            Decimal(
                resumo,
                "faturamentoTotal"));

        Assert.Equal(
            (decimal)deliveryEsperado,
            Decimal(
                resumo,
                "faturamentoDelivery"));

        Assert.Equal(
            (decimal)salaoEsperado,
            Decimal(
                resumo,
                "faturamentoSalao"));

        Assert.Equal(
            vendasEsperadas,
            Inteiro(
                resumo,
                "quantidadeVendas"));

        Assert.Equal(
            (decimal)totalEsperado /
                vendasEsperadas,
            Decimal(
                resumo,
                "ticketMedio"));

        // A soma dos canais deve corresponder
        // ao faturamento consolidado.
        Assert.Equal(
            (decimal)totalEsperado,
            Decimal(delivery, "faturamento") +
            Decimal(salao, "faturamento"));

        // A participação dos canais deve somar 100%.
        Assert.Equal(
            100m,
            Decimal(
                delivery,
                "participacaoPercentual") +
            Decimal(
                salao,
                "participacaoPercentual"));

        // Confere as quantidades no histórico.
        Assert.Equal(
            vendasEsperadas,
            Inteiro(
                vendas,
                "totalRegistros"));

        var registros = vendas
            .GetProperty("registros")
            .EnumerateArray()
            .ToList();

        Assert.Equal(
            vendasEsperadas,
            registros.Count);

        // O histórico deve corresponder
        // ao total financeiro do período.
        Assert.Equal(
            (decimal)totalEsperado,
            registros.Sum(r =>
                Decimal(r, "total")));

        if (canal != "todos")
        {
            Assert.All(
                registros,
                registro => Assert.Equal(
                    canal,
                    registro.GetProperty("canal")
                        .GetString()));
        }

        // Confere o gráfico de vendas diárias.
        var dias = graficos
            .GetProperty("vendasPorDia")
            .EnumerateArray()
            .ToList();

        Assert.Equal(17, dias.Count);

        Assert.Equal(
            "2026-11-01",
            dias.First()
                .GetProperty("data")
                .GetString());

        Assert.Equal(
            "2026-11-17",
            dias.Last()
                .GetProperty("data")
                .GetString());

        Assert.Equal(
            (decimal)totalEsperado,
            dias.Sum(d =>
                Decimal(d, "total")));

        // Confere o gráfico de movimento por hora.
        var horas = graficos
            .GetProperty("movimentoPorHora")
            .EnumerateArray()
            .ToList();

        Assert.Equal(24, horas.Count);

        Assert.Equal(
            vendasEsperadas,
            horas.Sum(h =>
                Inteiro(h, "totalVendas")));

        // Ranking dos produtos.
        var produto = Assert.Single(
            json.GetProperty("produtosMaisVendidos")
                .EnumerateArray()
                .ToList());

        Assert.Equal(
            unidadesEsperadas,
            Inteiro(produto, "quantidade"));

        // Reservas independem do canal financeiro.
        var reservas =
            json.GetProperty("reservas");

        Assert.Equal(
            1,
            Inteiro(reservas, "validas"));

        Assert.Equal(
            1,
            Inteiro(
                reservas,
                "canceladasOuExpiradas"));

        Assert.Equal(
            2,
            Inteiro(reservas, "total"));
    }

    // ==================================================
    // TESTE 3 — PAGINAÇÃO DO HISTÓRICO
    // ==================================================

    [Fact]
    public async Task HistoricoPaginadoPreservaResumoFinanceiro()
    {
        await using var host =
            await HostMobile.Criar();

        await DashboardAdminApiHttpTests
            .InserirDadosDeTeste(host);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pagina1 = await Consultar(
            http,
            Periodo +
            "&pagina=1&tamanhoPagina=1");

        var pagina2 = await Consultar(
            http,
            Periodo +
            "&pagina=2&tamanhoPagina=1");

        var pagina3 = await Consultar(
            http,
            Periodo +
            "&pagina=3&tamanhoPagina=1");

        var vendas1 =
            pagina1.GetProperty("vendas");

        var vendas2 =
            pagina2.GetProperty("vendas");

        var vendas3 =
            pagina3.GetProperty("vendas");

        Assert.Equal(
            1,
            Inteiro(vendas1, "pagina"));

        Assert.Equal(
            2,
            Inteiro(vendas2, "pagina"));

        Assert.Equal(
            3,
            Inteiro(vendas3, "pagina"));

        Assert.Equal(
            1,
            Inteiro(vendas1, "tamanhoPagina"));

        Assert.Equal(
            2,
            Inteiro(vendas1, "totalRegistros"));

        Assert.Equal(
            2,
            Inteiro(vendas1, "totalPaginas"));

        Assert.False(
            vendas1.GetProperty("temPaginaAnterior")
                .GetBoolean());

        Assert.True(
            vendas1.GetProperty("temProximaPagina")
                .GetBoolean());

        Assert.True(
            vendas2.GetProperty("temPaginaAnterior")
                .GetBoolean());

        Assert.False(
            vendas2.GetProperty("temProximaPagina")
                .GetBoolean());

        var registro1 = Assert.Single(
            vendas1.GetProperty("registros")
                .EnumerateArray()
                .ToList());

        var registro2 = Assert.Single(
            vendas2.GetProperty("registros")
                .EnumerateArray()
                .ToList());

        // O mesmo registro não deve ser
        // repetido nas páginas consecutivas.
        Assert.NotEqual(
            registro1.GetProperty("canal")
                .GetString(),
            registro2.GetProperty("canal")
                .GetString());

        Assert.Equal(
            100m,
            Decimal(registro1, "total") +
            Decimal(registro2, "total"));

        // Uma página além do total retorna
        // uma lista vazia, sem erro.
        Assert.Equal(
            0,
            vendas3.GetProperty("registros")
                .GetArrayLength());

        // O resumo não pode ser limitado
        // aos registros da página selecionada.
        foreach (
            var pagina in new[]
            {
                pagina1,
                pagina2,
                pagina3
            })
        {
            var resumo =
                pagina.GetProperty("resumo");

            Assert.Equal(
                100m,
                Decimal(
                    resumo,
                    "faturamentoTotal"));

            Assert.Equal(
                2,
                Inteiro(
                    resumo,
                    "quantidadeVendas"));
        }
    }

    // ==================================================
    // TESTE 4 — VALIDAÇÃO DE FILTROS
    // ==================================================

    [Theory]
    [InlineData("?inicio=2026-11-01")]
    [InlineData("?inicio=2026-11-20&fim=2026-11-01")]
    [InlineData("?inicio=2025-01-01&fim=2026-11-17")]
    [InlineData("?canal=invalido")]
    [InlineData("?pagina=0")]
    [InlineData("?tamanhoPagina=0")]
    [InlineData("?tamanhoPagina=101")]
    [InlineData("?compararInicio=2026-10-01")]
    public async Task FiltrosInvalidosRetornam400(
        string filtros)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.GetAsync(
                Rota + filtros);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            resposta.StatusCode);

        var json = await resposta.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.False(
            json.GetProperty("sucesso")
                .GetBoolean());

        Assert.False(
            string.IsNullOrWhiteSpace(
                json.GetProperty("mensagem")
                    .GetString()));
    }

    // ==================================================
    // TESTE 5 — COMPARAÇÃO ENTRE PERÍODOS
    // ==================================================

    [Fact]
    public async Task ComparacaoRespeitaPeriodoPersonalizado()
    {
        await using var host =
            await HostMobile.Criar();

        await DashboardAdminApiHttpTests
            .InserirDadosDeTeste(host);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        // O período comparativo contém
        // a venda fictícia de R$ 50,00.
        var json = await Consultar(
            http,
            Periodo +
            "&compararInicio=2026-10-10" +
            "&compararFim=2026-10-10");

        var comparacao =
            json.GetProperty("comparacao");

        var periodo =
            json.GetProperty("periodo");

        Assert.Equal(
            "2026-10-10",
            periodo.GetProperty("compararInicio")
                .GetString());

        Assert.Equal(
            "2026-10-10",
            periodo.GetProperty("compararFim")
                .GetString());

        Assert.Equal(
            100m,
            Decimal(
                comparacao,
                "faturamentoAtual"));

        Assert.Equal(
            50m,
            Decimal(
                comparacao,
                "faturamentoAnterior"));

        Assert.Equal(
            50m,
            Decimal(
                comparacao,
                "diferencaFaturamento"));

        Assert.Equal(
            100m,
            Decimal(
                comparacao,
                "variacaoFaturamentoPercentual"));

        Assert.Equal(
            2,
            Inteiro(
                comparacao,
                "quantidadeAtual"));

        Assert.Equal(
            1,
            Inteiro(
                comparacao,
                "quantidadeAnterior"));

        Assert.Equal(
            100m,
            Decimal(
                comparacao,
                "variacaoQuantidadePercentual"));

        // Seleciona um período anterior
        // sem nenhuma venda registrada.
        var semAnterior = await Consultar(
            http,
            Periodo +
            "&compararInicio=2026-10-11" +
            "&compararFim=2026-10-11");

        var novaComparacao =
            semAnterior.GetProperty("comparacao");

        Assert.Equal(
            0m,
            Decimal(
                novaComparacao,
                "faturamentoAnterior"));

        Assert.Equal(
            JsonValueKind.Null,
            novaComparacao.GetProperty(
                "variacaoFaturamentoPercentual")
                .ValueKind);

        Assert.Equal(
            JsonValueKind.Null,
            novaComparacao.GetProperty(
                "variacaoQuantidadePercentual")
                .ValueKind);
    }

    // ==================================================
    // TESTE 6 — CUPONS E DESCONTOS
    // ==================================================

    [Fact]
    public async Task RelatorioPreservaDescontoECodigoHistoricoDoCupom()
    {
        await using var host =
            await HostMobile.Criar();

        await DashboardAdminApiHttpTests
            .InserirDadosDeTeste(host);

        // Acrescenta um desconto fictício
        // à venda delivery de R$ 60,00.
        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var pedidos =
                await db.Pedidos.ToListAsync();

            var pedido = pedidos.Single(p =>
                p.ValorTotal == 60m &&
                p.StatusPedido ==
                    StatusPedido.Entregue);

            pedido.CodigoCupom =
                "MOBILETESTE";

            pedido.DescontoCupom = 5m;

            pedido.ValorTotal = 55m;

            await db.SaveChangesAsync();
        }

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var json = await Consultar(http);

        var resumo =
            json.GetProperty("resumo");

        var descontos =
            json.GetProperty("descontos");

        var delivery =
            json.GetProperty("delivery");

        Assert.Equal(
            95m,
            Decimal(
                resumo,
                "faturamentoTotal"));

        Assert.Equal(
            55m,
            Decimal(
                resumo,
                "faturamentoDelivery"));

        Assert.Equal(
            5m,
            Decimal(descontos, "cupons"));

        Assert.Equal(
            5m,
            Decimal(descontos, "total"));

        Assert.Equal(
            1,
            Inteiro(
                descontos,
                "pedidosComCupom"));

        Assert.Equal(
            1,
            Inteiro(
                delivery,
                "pedidosComCupom"));

        var cupons = json
            .GetProperty("cupons")
            .EnumerateArray()
            .ToList();

        var cupom = Assert.Single(
            cupons,
            c => c.GetProperty("codigo")
                .GetString() == "MOBILETESTE");

        Assert.Equal(
            1,
            Inteiro(cupom, "utilizados"));

        Assert.Equal(
            5m,
            Decimal(cupom, "descontos"));

        // Preserva o código mesmo sem existir
        // uma campanha atual com esse cadastro.
        Assert.Equal(
            JsonValueKind.Null,
            cupom.GetProperty("id")
                .ValueKind);

        // O canal salão não deve contabilizar
        // descontos de cupons do delivery.
        var apenasSalao = await Consultar(
            http,
            Periodo + "&canal=salao");

        Assert.Equal(
            0,
            apenasSalao.GetProperty("cupons")
                .GetArrayLength());

        Assert.Equal(
            0m,
            Decimal(
                apenasSalao.GetProperty("descontos"),
                "cupons"));

        Assert.Equal(
            40m,
            Decimal(
                apenasSalao.GetProperty("resumo"),
                "faturamentoTotal"));
    }

    // ==================================================
    // TESTE 7 — PERÍODO SEM VENDAS
    // ==================================================

    [Fact]
    public async Task PeriodoSemVendasRetornaRelatorioValido()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var json = await Consultar(http);

        var resumo =
            json.GetProperty("resumo");

        var vendas =
            json.GetProperty("vendas");

        var comparacao =
            json.GetProperty("comparacao");

        Assert.Equal(
            0m,
            Decimal(
                resumo,
                "faturamentoTotal"));

        Assert.Equal(
            0m,
            Decimal(
                resumo,
                "ticketMedio"));

        Assert.Equal(
            0,
            Inteiro(
                resumo,
                "quantidadeVendas"));

        Assert.Equal(
            0,
            Inteiro(
                vendas,
                "totalRegistros"));

        Assert.Equal(
            0,
            Inteiro(
                vendas,
                "totalPaginas"));

        Assert.Equal(
            0,
            vendas.GetProperty("registros")
                .GetArrayLength());

        Assert.False(
            vendas.GetProperty("temPaginaAnterior")
                .GetBoolean());

        Assert.False(
            vendas.GetProperty("temProximaPagina")
                .GetBoolean());

        Assert.Equal(
            JsonValueKind.Null,
            comparacao.GetProperty(
                "variacaoFaturamentoPercentual")
                .ValueKind);

        Assert.Equal(
            0,
            json.GetProperty("produtosMaisVendidos")
                .GetArrayLength());

        var graficos =
            json.GetProperty("graficos");

        Assert.Equal(
            17,
            graficos.GetProperty("vendasPorDia")
                .GetArrayLength());

        Assert.Equal(
            24,
            graficos.GetProperty("movimentoPorHora")
                .GetArrayLength());
    }
}
