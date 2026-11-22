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
/// Testes HTTP das operações administrativas
/// dos pedidos delivery para o Kiora Mobile.
///
/// Utiliza banco SQLite temporário,
/// autenticação JWT e pagamentos simulados.
///
/// Nenhuma operação financeira real é realizada.
/// </summary>
public sealed class PedidosAdminApiHttpTests
{
    private const string Rota =
        "/api/admin/pedidos";

    private const string Motivo =
        "Ingrediente indisponível para atendimento.";

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

    private static Task<HttpResponseMessage> Avancar(
        HttpClient http,
        int id,
        Guid versao,
        StatusPedido proximo)
    {
        return http.PostAsJsonAsync(
            $"{Rota}/{id}/avancar",
            new
            {
                versao,
                proximo = (int)proximo
            });
    }

    private static Task<HttpResponseMessage> Cancelar(
        HttpClient http,
        int id,
        Guid versao,
        string motivo = Motivo)
    {
        return http.PostAsJsonAsync(
            $"{Rota}/{id}/cancelar",
            new
            {
                versaoPedido = versao,
                motivo
            });
    }

    private static async Task<JsonElement> ConfirmarSucesso(
        HttpResponseMessage resposta)
    {
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
    public async Task OperacoesExigemAutenticacaoJwt()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        var pedido =
            await CriarPedido(host);

        // Sem autenticação.
        using var avancarAnonimo = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            avancarAnonimo.StatusCode);

        using var cancelarAnonimo = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            cancelarAnonimo.StatusCode);

        // Token inválido.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-invalido");

        using var tokenInvalido = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            tokenInvalido.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);
    }

    // ==================================================
    // TESTE 2 — PERMISSÕES DOS FUNCIONÁRIOS
    // ==================================================

    [Theory]
    [InlineData(TipoUsuario.Cliente)]
    [InlineData(TipoUsuario.Gerente)]
    [InlineData(TipoUsuario.Garcom)]
    [InlineData(TipoUsuario.Portaria)]
    [InlineData(TipoUsuario.Cozinha)]
    public async Task OutrosCargosNaoPodemGerenciarDelivery(
        TipoUsuario cargo)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        var pedido =
            await CriarPedido(host);

        Autenticar(http, host, cargo);

        using var avancar = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            avancar.StatusCode);

        using var cancelar = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            cancelar.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);

        Assert.Null(
            atual.OperacaoPendenteId);
    }

    // ==================================================
    // TESTE 3 — FLUXO COMPLETO DO PEDIDO
    // ==================================================

    [Fact]
    public async Task AdministradorAvancaPedidoUmaEtapaPorVez()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido =
            await CriarPedido(host);

        var versaoOriginal =
            pedido.Versao;

        // Recebido -> Em preparo.
        using var preparo = await Avancar(
            http,
            pedido.Id,
            versaoOriginal,
            StatusPedido.EmPreparo);

        var jsonPreparo =
            await ConfirmarSucesso(preparo);

        Assert.Equal(
            (int)StatusPedido.EmPreparo,
            jsonPreparo.GetProperty("situacao")
                .GetInt32());

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.EmPreparo,
            atual.StatusPedido);

        Assert.NotEqual(
            versaoOriginal,
            atual.Versao);

        // Impede avanço utilizando versão antiga.
        using var versaoInvalida = await Avancar(
            http,
            pedido.Id,
            versaoOriginal,
            StatusPedido.SaiuParaEntrega);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            versaoInvalida.StatusCode);

        // Impede saída antes de a cozinha
        // confirmar que os itens estão prontos.
        using var cozinhaPendente = await Avancar(
            http,
            pedido.Id,
            atual.Versao,
            StatusPedido.SaiuParaEntrega);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            cozinhaPendente.StatusCode);

        // Simula a conclusão do preparo
        // no banco temporário.
        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await db.ItensPedido
                .Where(i =>
                    i.PedidoId == pedido.Id)
                .ExecuteUpdateAsync(set =>
                    set.SetProperty(
                        i => i.SituacaoPreparo,
                        SituacaoPreparo.Pronto));
        }

        atual = await LerPedido(
            host,
            pedido.Id);

        // Em preparo -> Saiu para entrega.
        using var entrega = await Avancar(
            http,
            pedido.Id,
            atual.Versao,
            StatusPedido.SaiuParaEntrega);

        await ConfirmarSucesso(entrega);

        atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.SaiuParaEntrega,
            atual.StatusPedido);

        // Saiu para entrega -> Entregue.
        using var conclusao = await Avancar(
            http,
            pedido.Id,
            atual.Versao,
            StatusPedido.Entregue);

        await ConfirmarSucesso(conclusao);

        atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Entregue,
            atual.StatusPedido);

        // Pagamento em dinheiro é confirmado
        // na conclusão, conforme a regra existente.
        Assert.Equal(
            StatusPagamento.Aprovado,
            atual.StatusPagamento);

        Assert.Equal(
            pedido.ValorTotal,
            atual.ValorTotal);
    }

    // ==================================================
    // TESTE 4 — TRANSIÇÕES INVÁLIDAS
    // ==================================================

    [Fact]
    public async Task PedidoNaoPodePularEtapas()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido =
            await CriarPedido(host);

        // Recebido não pode ir diretamente
        // para SaiuParaEntrega.
        using var pularEtapa = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.SaiuParaEntrega);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            pularEtapa.StatusCode);

        // Cancelamento não pode ser executado
        // pelo endpoint de avanço.
        using var cancelarPorAvanco = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.Cancelado);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            cancelarPorAvanco.StatusCode);

        // Versão vazia.
        using var semVersao = await Avancar(
            http,
            pedido.Id,
            Guid.Empty,
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            semVersao.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);
    }

    // ==================================================
    // TESTE 5 — PAGAMENTO ONLINE PENDENTE
    // ==================================================

    [Fact]
    public async Task PagamentoOnlinePendenteImpedeAvanco()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido = await CriarPedido(
            host,
            formaPagamento:
                FormaPagamento.MercadoPago,

            pagamento:
                StatusPagamento.Pendente);

        using var resposta = await Avancar(
            http,
            pedido.Id,
            pedido.Versao,
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            resposta.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            StatusPagamento.Pendente,
            atual.StatusPagamento);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);
    }

    // ==================================================
    // TESTE 6 — CANCELAMENTO PERMITIDO
    // ==================================================

    [Theory]
    [InlineData(StatusPedido.Recebido)]
    [InlineData(StatusPedido.EmPreparo)]
    public async Task CancelamentoPreservaHistoricoEValores(
        StatusPedido etapa)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido = await CriarPedido(
            host,
            situacao: etapa);

        using var resposta = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        var json =
            await ConfirmarSucesso(resposta);

        Assert.True(
            json.GetProperty("concluido")
                .GetBoolean());

        Assert.False(
            json.GetProperty("emConferencia")
                .GetBoolean());

        Assert.False(
            json.GetProperty("reembolsoPendente")
                .GetBoolean());

        Assert.Equal(
            (int)StatusPedido.Cancelado,
            json.GetProperty("situacao")
                .GetInt32());

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Cancelado,
            atual.StatusPedido);

        Assert.Equal(
            StatusPagamento.Cancelado,
            atual.StatusPagamento);

        Assert.Equal(
            pedido.ValorTotal,
            atual.ValorTotal);

        Assert.Equal(
            pedido.TaxaEntrega,
            atual.TaxaEntrega);

        Assert.Equal(
            Motivo,
            atual.MotivoCancelamento);

        Assert.NotNull(
            atual.CanceladoEmUtc);

        Assert.Null(
            atual.OperacaoPendenteId);

        Assert.NotEqual(
            pedido.Versao,
            atual.Versao);

        // Repetir a solicitação não deve criar
        // outro cancelamento ou aviso de cozinha.
        using var repeticao = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        await ConfirmarSucesso(repeticao);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var operacao = Assert.Single(
            await db.OperacoesPedido
                .Where(o =>
                    o.PedidoId == pedido.Id)
                .ToListAsync());

        Assert.Equal(
            EstadoOperacaoPedido.Concluida,
            operacao.Estado);

        Assert.NotNull(
            operacao.AdministradorSolicitanteId);

        Assert.Single(
            await db.Notificacoes
                .Where(n =>
                    n.PedidoId == pedido.Id)
                .ToListAsync());
    }

    // ==================================================
    // TESTE 7 — CANCELAMENTO NÃO PERMITIDO
    // ==================================================

    [Theory]
    [InlineData(StatusPedido.SaiuParaEntrega)]
    [InlineData(StatusPedido.Entregue)]
    public async Task NaoCancelaPedidoAposSaidaParaEntrega(
        StatusPedido etapa)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido = await CriarPedido(
            host,
            situacao: etapa);

        using var resposta = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            resposta.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            etapa,
            atual.StatusPedido);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);

        Assert.Null(
            atual.OperacaoPendenteId);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Empty(
            await db.OperacoesPedido
                .ToListAsync());
    }

    // ==================================================
    // TESTE 8 — PEDIDO INEXISTENTE
    // ==================================================

    [Fact]
    public async Task PedidoInexistenteRetorna404()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var avancar = await Avancar(
            http,
            int.MaxValue,
            Guid.NewGuid(),
            StatusPedido.EmPreparo);

        Assert.Equal(
            HttpStatusCode.NotFound,
            avancar.StatusCode);

        using var cancelar = await Cancelar(
            http,
            int.MaxValue,
            Guid.NewGuid());

        Assert.Equal(
            HttpStatusCode.NotFound,
            cancelar.StatusCode);
    }

    // ==================================================
    // TESTE 9 — VALIDAÇÃO DO CANCELAMENTO
    // ==================================================

    [Fact]
    public async Task CancelamentoExigeMotivoEVersaoAtual()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido =
            await CriarPedido(host);

        // Motivo obrigatório.
        using var semMotivo = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao,
            "");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            semMotivo.StatusCode);

        // Motivo acima do limite.
        using var motivoLongo = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao,
            new string('a', 301));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            motivoLongo.StatusCode);

        // Versão desatualizada.
        using var versaoAntiga = await Cancelar(
            http,
            pedido.Id,
            Guid.NewGuid());

        Assert.Equal(
            HttpStatusCode.BadRequest,
            versaoAntiga.StatusCode);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            pedido.Versao,
            atual.Versao);

        Assert.Null(
            atual.OperacaoPendenteId);
    }

    // ==================================================
    // TESTE 10 — PAGAMENTO ONLINE SIMULADO
    // ==================================================

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelamentoOnlineNaoPrometeEstorno(
        bool pago)
    {
        // Reutiliza o simulador financeiro
        // já existente na suíte de testes.
        var provedor =
            new GerenciarPedidoTests.Provedor();

        await using var host =
            await HostMobile.Criar(
                provedorPagamento: provedor);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido = await CriarPedidoOnline(
            host,
            provedor,
            pago);

        using var resposta = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        var json =
            await ConfirmarSucesso(resposta);

        Assert.True(
            json.GetProperty("concluido")
                .GetBoolean());

        Assert.Equal(
            pago,
            json.GetProperty("reembolsoPendente")
                .GetBoolean());

        Assert.True(
            provedor.Consultas > 0);

        // Pagamento aprovado não significa
        // que ocorreu devolução financeira.
        Assert.Equal(
            0,
            provedor.Devolucoes);

        // Uma cobrança ainda não paga pode
        // ser cancelada no provedor simulado.
        Assert.Equal(
            pago ? 0 : 1,
            provedor.Cancelamentos);

        var atual = await LerPedido(
            host,
            pedido.Id);

        Assert.Equal(
            StatusPedido.Cancelado,
            atual.StatusPedido);

        Assert.Equal(
            pago
                ? StatusPagamento.Aprovado
                : StatusPagamento.Cancelado,
            atual.StatusPagamento);

        Assert.Equal(
            pago,
            atual.ReembolsoCancelamentoPendente);

        Assert.Equal(
            pedido.ValorTotal,
            atual.ValorTotal);
    }

    // ==================================================
    // TESTE 11 — CANCELAMENTO EM CONFERÊNCIA
    // ==================================================

    [Fact]
    public async Task FalhaDoProvedorRetorna202SemCancelarPedido()
    {
        var provedor =
            new GerenciarPedidoTests.Provedor();

        await using var host =
            await HostMobile.Criar(
                provedorPagamento: provedor);

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var pedido = await CriarPedidoOnline(
            host,
            provedor,
            pago: true);

        // Simula indisponibilidade externa.
        provedor.AoConsultar = () =>
            throw new HttpRequestException(
                "Falha financeira simulada.");

        using var resposta = await Cancelar(
            http,
            pedido.Id,
            pedido.Versao);

        Assert.Equal(
            HttpStatusCode.Accepted,
            resposta.StatusCode);

        var json = await resposta.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.False(
            json.GetProperty("sucesso")
                .GetBoolean());

        Assert.False(
            json.GetProperty("concluido")
                .GetBoolean());

        Assert.True(
            json.GetProperty("emConferencia")
                .GetBoolean());

        Assert.True(
            json.GetProperty("atualizarDetalhes")
                .GetBoolean());

        var atual = await LerPedido(
            host,
            pedido.Id);

        // O pedido não pode aparecer como
        // cancelado antes da confirmação.
        Assert.Equal(
            StatusPedido.Recebido,
            atual.StatusPedido);

        Assert.Equal(
            StatusPagamento.Aprovado,
            atual.StatusPagamento);

        Assert.NotNull(
            atual.OperacaoPendenteId);

        Assert.Equal(
            0,
            provedor.Devolucoes);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var operacao = Assert.Single(
            await db.OperacoesPedido
                .Where(o =>
                    o.PedidoId == pedido.Id)
                .ToListAsync());

        Assert.Equal(
            EstadoOperacaoPedido.Conferencia,
            operacao.Estado);

        // Não deve avisar a cozinha sobre
        // um cancelamento ainda não confirmado.
        Assert.Empty(
            await db.Notificacoes
                .Where(n =>
                    n.PedidoId == pedido.Id)
                .ToListAsync());
    }

    // ==================================================
    // DADOS FICTÍCIOS DOS TESTES
    // ==================================================

    private static async Task<Pedido> CriarPedido(
        HostMobile host,

        StatusPedido situacao =
            StatusPedido.Recebido,

        FormaPagamento formaPagamento =
            FormaPagamento.Dinheiro,

        StatusPagamento pagamento =
            StatusPagamento.Pendente)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var cliente = await db.Usuarios
            .SingleAsync(u =>
                u.Email == "cliente@mobile.test");

        var produto = await db.Produtos
            .OrderBy(p => p.Id)
            .FirstAsync();

        var pedido = new Pedido
        {
            UsuarioId = cliente.Id,

            DataPedido = new DateTime(
                2026, 11, 17, 14, 0, 0,
                DateTimeKind.Utc),

            FormaPagamento = formaPagamento,

            StatusPagamento = pagamento,

            StatusPedido = situacao,

            ValorTotal = 45m,

            TaxaEntrega = 5m,

            PreparoMinutos = 30,

            DuracaoPercursoSegundos = 600,

            EnderecoEntrega = new EnderecoEntrega
            {
                Cep = "05422000",

                Logradouro =
                    "Rua fictícia de teste",

                Numero = "100",

                Bairro = "Pinheiros",

                Cidade = "São Paulo",

                Uf = "SP"
            },

            ItensPedido =
            [
                new ItemPedido
                {
                    ProdutoId = produto.Id,

                    NomeProduto =
                        "Produto fictício de teste",

                    Quantidade = 1,

                    PrecoUnitario = 40m,

                    PrecoOriginalUnitario = 40m,

                    SituacaoPreparo =
                        SituacaoPreparo.NaFila
                }
            ]
        };

        db.Pedidos.Add(pedido);

        await db.SaveChangesAsync();

        return pedido;
    }

    // ==================================================
    // PEDIDO COM PAGAMENTO ONLINE SIMULADO
    // ==================================================

    private static async Task<Pedido> CriarPedidoOnline(
        HostMobile host,
        GerenciarPedidoTests.Provedor provedor,
        bool pago)
    {
        var pedido = await CriarPedido(
            host,

            formaPagamento:
                FormaPagamento.MercadoPago,

            pagamento:
                pago
                    ? StatusPagamento.Aprovado
                    : StatusPagamento.Pendente);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var registro = new PagamentoOnline
        {
            PedidoId = pedido.Id,

            Valor = pedido.ValorTotal
        };

        registro.CorpoCriacao =
            JsonSerializer.Serialize(new
            {
                external_reference =
                    registro.Referencia.ToString("N"),

                total_amount = "45.00"
            });

        // Inicializa uma ordem fictícia.
        provedor.Inicializar(
            registro.Referencia,
            registro.Valor,
            pago);

        registro.OrderId =
            provedor.Principal.Id;

        db.PagamentosOnline.Add(registro);

        await db.SaveChangesAsync();

        return pedido;
    }

    // ==================================================
    // CONSULTA DIRETA AO BANCO DE TESTES
    // ==================================================

    private static async Task<Pedido> LerPedido(
        HostMobile host,
        int id)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await db.Pedidos
            .AsNoTracking()
            .Include(p => p.ItensPedido)
            .SingleAsync(p => p.Id == id);
    }
}
