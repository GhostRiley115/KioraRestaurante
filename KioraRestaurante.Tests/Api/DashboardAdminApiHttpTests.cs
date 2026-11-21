
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

/// <summary>
/// Testes HTTP do dashboard administrativo
/// consolidado para o Kiora Mobile.
///
/// Utiliza banco SQLite temporário,
/// autenticação JWT e os serviços reais
/// de administração e relatórios.
///
/// Não utiliza o banco de produção.
/// </summary>
public sealed class DashboardAdminApiHttpTests
{
    private const string Rota =
        "/api/admin/dashboard";

    private const string Periodo =
        "?inicio=2026-11-01&fim=2026-11-17";

    // ==================================================
    // MÉTODOS AUXILIARES
    // ==================================================

    private static void Autenticar(
        HttpClient http,
        HostMobile host,
        TipoUsuario cargo)
    {
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                host.Token(cargo));
    }

    private static async Task<JsonElement> ConsultarDashboard(
        HttpClient http,
        string filtros = "")
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

    // ==================================================
    // TESTE 1 — AUTENTICAÇÃO OBRIGATÓRIA
    // ==================================================

    [Fact]
    public async Task DashboardExigeTokenJwtValido()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        // Requisição sem autenticação.
        using var anonimo =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonimo.StatusCode);

        // Requisição com token inválido.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-invalido");

        using var invalido =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            invalido.StatusCode);
    }

    // ==================================================
    // TESTE 2 — PERMISSÕES POR CARGO
    // ==================================================

    [Fact]
    public async Task SomenteAdministradorAcessaDashboard()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

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
    // TESTE 3 — DASHBOARD SEM MOVIMENTO
    // ==================================================

    [Fact]
    public async Task DashboardSemVendasRetornaValoresZerados()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(
            http,
            host,
            TipoUsuario.Administrador);

        var json =
            await ConsultarDashboard(http);

        var financeiro =
            json.GetProperty("financeiro");

        var salao =
            json.GetProperty("salao");

        var reservas =
            json.GetProperty("reservas");

        var usuarios =
            json.GetProperty("usuarios");

        var graficos =
            json.GetProperty("graficos");

        var periodo =
            json.GetProperty("periodo");

        // Não existem vendas no banco inicial.
        Assert.Equal(
            0m,
            financeiro
                .GetProperty("faturamentoTotal")
                .GetDecimal());

        Assert.Equal(
            0m,
            financeiro
                .GetProperty("ticketMedio")
                .GetDecimal());

        Assert.Equal(
            0,
            financeiro
                .GetProperty("quantidadeVendas")
                .GetInt32());

        // Não deve ocorrer divisão por zero.
        Assert.Equal(
            JsonValueKind.Null,
            financeiro
                .GetProperty("variacaoPercentual")
                .ValueKind);

        // O banco inicial possui 12 mesas.
        Assert.Equal(
            12,
            salao
                .GetProperty("totalMesas")
                .GetInt32());

        Assert.Equal(
            12,
            salao
                .GetProperty("mesasLivres")
                .GetInt32());

        Assert.Equal(
            0,
            salao
                .GetProperty("mesasOcupadas")
                .GetInt32());

        Assert.Equal(
            0,
            reservas
                .GetProperty("total")
                .GetInt32());

        Assert.Equal(
            2,
            usuarios
                .GetProperty("clientesAtivos")
                .GetInt32());

        Assert.Equal(
            5,
            usuarios
                .GetProperty("funcionariosAtivos")
                .GetInt32());

        // O relógio de HostMobile permanece fixo
        // em 17/11/2026.
        Assert.Equal(
            "2026-11-01",
            periodo
                .GetProperty("inicio")
                .GetString());

        Assert.Equal(
            "2026-11-17",
            periodo
                .GetProperty("fim")
                .GetString());

        Assert.Equal(
            "America/Sao_Paulo",
            periodo
                .GetProperty("fusoHorario")
                .GetString());

        Assert.Equal(
            17,
            graficos
                .GetProperty("vendasPorDia")
                .GetArrayLength());

        Assert.Equal(
            24,
            graficos
                .GetProperty("movimentoPorHora")
                .GetArrayLength());

        Assert.Equal(
            0,
            graficos
                .GetProperty("produtosMaisVendidos")
                .GetArrayLength());
    }

    // ==================================================
    // TESTE 4 — VALIDAÇÃO DOS FILTROS
    // ==================================================

    [Theory]
    [InlineData("?inicio=2026-11-01")]
    [InlineData("?inicio=2026-11-20&fim=2026-11-01")]
    [InlineData("?inicio=2025-01-01&fim=2026-11-17")]
    [InlineData("?canal=invalido")]
    [InlineData("?canal=delivery")]
    public async Task FiltrosInvalidosRetornam400(
        string filtros)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(
            http,
            host,
            TipoUsuario.Administrador);

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
    }

    // ==================================================
    // TESTE 5 — INDICADORES FINANCEIROS E OPERACIONAIS
    // ==================================================

    [Fact]
    public async Task DashboardConsolidaVendasSalaoDeliveryEReservas()
    {
        await using var host =
            await HostMobile.Criar();

        await InserirDadosDeTeste(host);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(
            http,
            host,
            TipoUsuario.Administrador);

        var json = await ConsultarDashboard(
            http,
            Periodo);

        var financeiro =
            json.GetProperty("financeiro");

        var pedidos =
            json.GetProperty("pedidos");

        var salao =
            json.GetProperty("salao");

        var reservas =
            json.GetProperty("reservas");

        var graficos =
            json.GetProperty("graficos");

        // ==============================================
        // FATURAMENTO
        // ==============================================

        // Delivery: R$ 60,00.
        // Salão: R$ 40,00.
        // Total: R$ 100,00.
        //
        // Pedidos cancelados, pendentes e vendas
        // fora do período não entram no faturamento.

        Assert.Equal(
            100m,
            financeiro
                .GetProperty("faturamentoTotal")
                .GetDecimal());

        Assert.Equal(
            60m,
            financeiro
                .GetProperty("faturamentoDelivery")
                .GetDecimal());

        Assert.Equal(
            40m,
            financeiro
                .GetProperty("faturamentoSalao")
                .GetDecimal());

        Assert.Equal(
            2,
            financeiro
                .GetProperty("quantidadeVendas")
                .GetInt32());

        Assert.Equal(
            1,
            financeiro
                .GetProperty("quantidadeVendasDelivery")
                .GetInt32());

        Assert.Equal(
            1,
            financeiro
                .GetProperty("quantidadeVendasSalao")
                .GetInt32());

        Assert.Equal(
            50m,
            financeiro
                .GetProperty("ticketMedio")
                .GetDecimal());

        Assert.Equal(
            60m,
            financeiro
                .GetProperty("ticketMedioDelivery")
                .GetDecimal());

        Assert.Equal(
            40m,
            financeiro
                .GetProperty("ticketMedioSalao")
                .GetDecimal());

        Assert.Equal(
            5m,
            financeiro
                .GetProperty("totalFrete")
                .GetDecimal());

        Assert.Equal(
            95m,
            financeiro
                .GetProperty("produtosLiquidos")
                .GetDecimal());

        // ==============================================
        // COMPARAÇÃO COM O PERÍODO ANTERIOR
        // ==============================================

        Assert.Equal(
            50m,
            financeiro
                .GetProperty("faturamentoAnterior")
                .GetDecimal());

        // R$ 50,00 para R$ 100,00:
        // crescimento de 100%.
        Assert.Equal(
            100m,
            financeiro
                .GetProperty("variacaoPercentual")
                .GetDecimal());

        // ==============================================
        // PEDIDOS
        // ==============================================

        Assert.Equal(
            5,
            pedidos
                .GetProperty("totalHistorico")
                .GetInt32());

        Assert.Equal(
            1,
            pedidos
                .GetProperty("aguardandoPagamento")
                .GetInt32());

        // ==============================================
        // SALÃO
        // ==============================================

        Assert.Equal(
            12,
            salao
                .GetProperty("totalMesas")
                .GetInt32());

        Assert.Equal(
            11,
            salao
                .GetProperty("mesasLivres")
                .GetInt32());

        Assert.Equal(
            1,
            salao
                .GetProperty("mesasOcupadas")
                .GetInt32());

        Assert.Equal(
            1,
            salao
                .GetProperty("comandasAbertas")
                .GetInt32());

        Assert.Equal(
            1,
            salao
                .GetProperty("comandasEncerradas")
                .GetInt32());

        Assert.Equal(
            1,
            salao
                .GetProperty("gruposNaFila")
                .GetInt32());

        // ==============================================
        // RESERVAS
        // ==============================================

        // A fila presencial não deve ser somada
        // às reservas agendadas do período.
        Assert.Equal(
            2,
            reservas
                .GetProperty("total")
                .GetInt32());

        Assert.Equal(
            1,
            reservas
                .GetProperty("aguardandoChegada")
                .GetInt32());

        Assert.Equal(
            1,
            reservas
                .GetProperty("canceladas")
                .GetInt32());

        // ==============================================
        // GRÁFICOS
        // ==============================================

        var dias = graficos
            .GetProperty("vendasPorDia")
            .EnumerateArray()
            .ToList();

        Assert.Equal(
            17,
            dias.Count);

        // A soma do gráfico deve corresponder
        // ao faturamento consolidado.
        var totalGrafico = dias.Sum(dia =>
            dia.GetProperty("delivery").GetDecimal() +
            dia.GetProperty("salao").GetDecimal());

        Assert.Equal(
            100m,
            totalGrafico);

        Assert.Equal(
            24,
            graficos
                .GetProperty("movimentoPorHora")
                .GetArrayLength());

        // Duas unidades vendidas no delivery
        // e uma unidade vendida no salão.
        var produto = Assert.Single(
            graficos
                .GetProperty("produtosMaisVendidos")
                .EnumerateArray()
                .ToList());

        Assert.Equal(
            3,
            produto
                .GetProperty("quantidade")
                .GetInt32());

        Assert.Equal(
            2,
            produto
                .GetProperty("delivery")
                .GetInt32());

        Assert.Equal(
            1,
            produto
                .GetProperty("salao")
                .GetInt32());
    }

    // ==================================================
    // TESTE 6 — ADMINISTRADOR DESATIVADO
    // ==================================================

    [Fact]
    public async Task AdministradorDesativadoPerdeAcessoAoDashboard()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(
            http,
            host,
            TipoUsuario.Administrador);

        await ConsultarDashboard(http);

        // Desativa o administrador somente
        // no banco temporário deste teste.
        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var administrador =
                await db.Usuarios.SingleAsync(
                    u => u.Tipo ==
                        TipoUsuario.Administrador);

            administrador.Ativo = false;

            administrador.VersaoSessao =
                Guid.NewGuid();

            await db.SaveChangesAsync();
        }

        // O token emitido anteriormente
        // não pode continuar permitindo acesso.
        using var resposta =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            resposta.StatusCode);
    }

    // ==================================================
    // DADOS FICTÍCIOS EXCLUSIVOS DOS TESTES
    // ==================================================

    private static async Task InserirDadosDeTeste(
        HostMobile host)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var cliente = await db.Usuarios
            .SingleAsync(
                u => u.Email == "cliente@mobile.test");

        var garcom = await db.Usuarios
            .SingleAsync(
                u => u.Tipo == TipoUsuario.Garcom);

        var produto = await db.Produtos
            .OrderBy(p => p.Id)
            .FirstAsync();

        var dataAtual = new DateTime(
            2026, 11, 17, 14, 0, 0,
            DateTimeKind.Utc);

        var dataAnterior = new DateTime(
            2026, 10, 10, 14, 0, 0,
            DateTimeKind.Utc);

        var foraDoPeriodo = new DateTime(
            2026, 11, 18, 3, 0, 0,
            DateTimeKind.Utc);

        // ==============================================
        // VENDAS DELIVERY
        // ==============================================

        // Venda válida do período atual.
        db.Pedidos.Add(
            CriarPedido(
                cliente.Id,
                produto.Id,
                dataAtual,
                60m,
                StatusPedido.Entregue,
                StatusPagamento.Aprovado,
                quantidade: 2,
                frete: 5m));

        // Venda válida do período anterior.
        db.Pedidos.Add(
            CriarPedido(
                cliente.Id,
                produto.Id,
                dataAnterior,
                50m,
                StatusPedido.Entregue,
                StatusPagamento.Aprovado));

        // Pagamento pendente: não entra na receita.
        db.Pedidos.Add(
            CriarPedido(
                cliente.Id,
                produto.Id,
                dataAtual,
                900m,
                StatusPedido.Recebido,
                StatusPagamento.Pendente));

        // Pedido cancelado: não entra na receita.
        db.Pedidos.Add(
            CriarPedido(
                cliente.Id,
                produto.Id,
                dataAtual,
                700m,
                StatusPedido.Cancelado,
                StatusPagamento.Aprovado));

        // Venda fora do período selecionado.
        db.Pedidos.Add(
            CriarPedido(
                cliente.Id,
                produto.Id,
                foraDoPeriodo,
                80m,
                StatusPedido.Entregue,
                StatusPagamento.Aprovado));

        // ==============================================
        // COMANDA ENCERRADA DO SALÃO
        // ==============================================

        db.AtendimentosMesa.Add(
            new AtendimentoMesa
            {
                MesaId = 1,
                NomeMesa = "Mesa 1",
                GarcomId = garcom.Id,
                NomeGrupo = "Grupo teste encerrado",
                Pessoas = 2,

                AbertoEmUtc =
                    dataAtual.AddHours(-1),

                FechadoEmUtc =
                    dataAtual,

                LiberadoEmUtc =
                    dataAtual.AddMinutes(10),

                Situacao =
                    SituacaoAtendimento.Encerrado,

                SubtotalFechamento = 40m,

                TotalFechamento = 40m,

                Itens =
                [
                    new ItemComanda
                    {
                        ProdutoId = produto.Id,

                        NomeProduto =
                            "Produto de teste",

                        Quantidade = 1,

                        PrecoUnitario = 40m,

                        PrecoOriginalUnitario = 40m,

                        Situacao =
                            SituacaoPreparo.Entregue,

                        EnviadoEmUtc =
                            dataAtual.AddMinutes(-30)
                    }
                ],

                Pagamentos =
                [
                    new PagamentoPresencial
                    {
                        Meio =
                            MeioPagamentoPresencial.Pix,

                        Valor = 40m
                    }
                ]
            });

        // ==============================================
        // COMANDA ABERTA
        // ==============================================

        db.AtendimentosMesa.Add(
            new AtendimentoMesa
            {
                MesaId = 2,

                MesaOcupadaId = 2,

                NomeMesa = "Mesa 2",

                GarcomId = garcom.Id,

                NomeGrupo = "Grupo teste ativo",

                Pessoas = 2,

                AbertoEmUtc = dataAtual,

                Situacao =
                    SituacaoAtendimento.Aberto
            });

        // ==============================================
        // RESERVA AGENDADA ATIVA
        // ==============================================

        db.Reservas.Add(
            new Reserva
            {
                ChaveSolicitacao =
                    Guid.NewGuid(),

                Nome = "Reserva de teste",

                Telefone = "11900000000",

                Pessoas = 2,

                Tipo = TipoReserva.Agendada,

                Situacao =
                    SituacaoReserva.AguardandoChegada,

                CriadaEmUtc =
                    dataAtual.AddDays(-1),

                InicioUtc =
                    dataAtual.AddHours(3),

                FimUtc =
                    dataAtual.AddHours(5),

                ExpiraEmUtc =
                    dataAtual.AddHours(6)
            });

        // ==============================================
        // RESERVA CANCELADA
        // ==============================================

        db.Reservas.Add(
            new Reserva
            {
                ChaveSolicitacao =
                    Guid.NewGuid(),

                Nome = "Reserva teste cancelada",

                Telefone = "11900000000",

                Pessoas = 2,

                Tipo = TipoReserva.Agendada,

                Situacao =
                    SituacaoReserva.Cancelada,

                CriadaEmUtc =
                    dataAtual.AddDays(-1),

                InicioUtc =
                    dataAtual,

                FimUtc =
                    dataAtual.AddHours(2),

                ExpiraEmUtc =
                    dataAtual.AddHours(3),

                EncerradaEmUtc =
                    dataAtual.AddMinutes(-10)
            });

        // ==============================================
        // GRUPO NA FILA PRESENCIAL
        // ==============================================

        db.Reservas.Add(
            new Reserva
            {
                ChaveSolicitacao =
                    Guid.NewGuid(),

                Nome = "Grupo teste fila",

                Telefone = "11900000000",

                Pessoas = 2,

                Tipo = TipoReserva.Fila,

                Situacao =
                    SituacaoReserva.Aguardando,

                CriadaEmUtc = dataAtual,

                ExpiraEmUtc =
                    dataAtual.AddHours(3)
            });

        await db.SaveChangesAsync();
    }

    // ==================================================
    // CRIA UM PEDIDO DELIVERY FICTÍCIO
    // ==================================================

    private static Pedido CriarPedido(
        int usuarioId,
        int produtoId,
        DateTime data,
        decimal total,
        StatusPedido situacao,
        StatusPagamento pagamento,
        int quantidade = 1,
        decimal frete = 0m)
    {
        var precoUnitario =
            (total - frete) / quantidade;

        return new Pedido
        {
            UsuarioId = usuarioId,

            DataPedido = data,

            ValorTotal = total,

            TaxaEntrega = frete,

            FormaPagamento =
                FormaPagamento.MercadoPago,

            StatusPedido = situacao,

            StatusPagamento = pagamento,

            EnderecoEntrega =
                new EnderecoEntrega
                {
                    Cep = "00000-000",

                    Logradouro =
                        "Rua fictícia de teste",

                    Numero = "100",

                    Bairro = "Bairro teste",

                    Cidade = "São Paulo",

                    Uf = "SP"
                },

            ItensPedido =
            [
                new ItemPedido
                {
                    ProdutoId = produtoId,

                    NomeProduto =
                        "Produto de teste",

                    Quantidade = quantidade,

                    PrecoUnitario =
                        precoUnitario,

                    PrecoOriginalUnitario =
                        precoUnitario
                }
            ]
        };
    }
}
