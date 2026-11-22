using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

// Testes HTTP do gerenciamento administrativo dos cupons de desconto do Kiora Mobile.
// Utiliza banco SQLite temporário, autenticação JWT e o serviço comercial real.
// Nenhuma cobrança ou operação financeira externa é executada.
public sealed class CuponsAdminApiHttpTests
{
    private const string Rota =
        "/api/admin/cupons";

    // MÉTODOS AUXILIARES
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

    private static Dictionary<string, object?> Dados(
        string codigo = "KIORA15")
    {
        return new Dictionary<string, object?>
        {
            ["codigo"] = codigo,

            ["descricao"] =
                "Cupom fictício de teste",

            ["parceiro"] =
                "Parceiro original",

            ["tipo"] =
                (int)TipoDescontoCupom.Percentual,

            ["valor"] = 15m,

            ["valorMinimoProdutos"] = 50m,

            ["descontoMaximo"] = 30m,

            ["inicioLocal"] =
                "2026-11-01T00:00:00",

            ["fimLocal"] =
                "2026-12-31T23:59:59",

            ["limiteTotal"] = 100,

            ["limitePorCliente"] = null,

            ["ativo"] = true
        };
    }

    private static async Task<JsonElement> LerResposta(
        HttpResponseMessage resposta,
        HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var corpo =
            await resposta.Content.ReadAsStringAsync();

        Assert.True(
            resposta.StatusCode == esperado,
            $"Esperado: {(int)esperado}. " +
            $"Recebido: {(int)resposta.StatusCode}. " +
            $"Resposta: {corpo}");

        using var documento =
            JsonDocument.Parse(corpo);

        var json =
            documento.RootElement.Clone();

        Assert.True(
            json.GetProperty("sucesso")
                .GetBoolean());

        return json;
    }

    private static async Task ConfirmarErro(
        HttpResponseMessage resposta,
        HttpStatusCode esperado =
            HttpStatusCode.BadRequest)
    {
        Assert.Equal(
            esperado,
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

    private static async Task<int> Cadastrar(
        HttpClient http,
        Dictionary<string, object?>? dados = null)
    {
        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                dados ?? Dados());

        var json = await LerResposta(
            resposta,
            HttpStatusCode.Created);

        var id =
            json.GetProperty("cupomId")
                .GetInt32();

        Assert.True(id > 0);

        Assert.NotNull(
            resposta.Headers.Location);

        Assert.EndsWith(
            $"{Rota}/{id}",
            resposta.Headers.Location!.ToString());

        return id;
    }

    private static async Task<JsonElement> Buscar(
        HttpClient http,
        int id)
    {
        using var resposta =
            await http.GetAsync(
                $"{Rota}/{id}");

        var json =
            await LerResposta(resposta);

        return json.GetProperty("cupom");
    }

    private static async Task<JsonElement> Relatorio(
        HttpClient http,
        int id,
        int pagina = 1)
    {
        using var resposta =
            await http.GetAsync(
                $"{Rota}/{id}/relatorio" +
                $"?pagina={pagina}");

        return await LerResposta(resposta);
    }

    private static Guid Versao(
        JsonElement cupom)
    {
        return cupom.GetProperty("versao")
            .GetGuid();
    }

    private static Task<HttpResponseMessage>
        AlterarAtivo(
            HttpClient http,
            int id,
            Guid versao,
            bool ativo)
    {
        return http.PatchAsJsonAsync(
            $"{Rota}/{id}/ativo",

            new
            {
                versao,
                ativo
            });
    }

    // TESTE 1 — AUTENTICAÇÃO OBRIGATÓRIA

    [Fact]
    public async Task CuponsAdminExigemAutenticacao()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        using var listar =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            listar.StatusCode);

        using var buscar =
            await http.GetAsync($"{Rota}/1");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            buscar.StatusCode);

        using var cadastrar =
            await http.PostAsJsonAsync(
                Rota,
                Dados());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            cadastrar.StatusCode);

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/1",
                Dados());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            editar.StatusCode);

        using var alterar =
            await AlterarAtivo(
                http,
                1,
                Guid.NewGuid(),
                false);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            alterar.StatusCode);

        using var relatorio =
            await http.GetAsync(
                $"{Rota}/1/relatorio");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            relatorio.StatusCode);

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
    }

    // TESTE 2 — PERMISSÕES POR CARGO

    [Theory]
    [InlineData(TipoUsuario.Cliente)]
    [InlineData(TipoUsuario.Gerente)]
    [InlineData(TipoUsuario.Garcom)]
    [InlineData(TipoUsuario.Portaria)]
    [InlineData(TipoUsuario.Cozinha)]
    public async Task OutrosCargosNaoAdministramCupons(
        TipoUsuario cargo)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host, cargo);

        using var listar =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            listar.StatusCode);

        using var buscar =
            await http.GetAsync($"{Rota}/1");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            buscar.StatusCode);

        using var cadastrar =
            await http.PostAsJsonAsync(
                Rota,
                Dados());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            cadastrar.StatusCode);

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/1",
                Dados());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            editar.StatusCode);

        using var alterar =
            await AlterarAtivo(
                http,
                1,
                Guid.NewGuid(),
                false);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            alterar.StatusCode);

        using var relatorio =
            await http.GetAsync(
                $"{Rota}/1/relatorio");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            relatorio.StatusCode);

        // Nenhum cupom deve ser persistido.
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Empty(
            await db.Cupons.ToListAsync());
    }

    // TESTE 3 — CADASTRO PERCENTUAL E FIXO

    [Theory]
    [InlineData(TipoDescontoCupom.Percentual)]
    [InlineData(TipoDescontoCupom.ValorFixo)]
    public async Task CadastroPreservaValoresEValidade(
        TipoDescontoCupom tipo)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var dados = Dados(
            "  kiora-teste  ");

        var valor =
            tipo == TipoDescontoCupom.Percentual
                ? 15m
                : 20.50m;

        dados["tipo"] = (int)tipo;

        dados["valor"] = valor;

        dados["limitePorCliente"] = 2;

        var id =
            await Cadastrar(http, dados);

        var cupom =
            await Buscar(http, id);

        // Código normalizado.
        Assert.Equal(
            "KIORA-TESTE",
            cupom.GetProperty("codigo")
                .GetString());

        Assert.Equal(
            (int)tipo,
            cupom.GetProperty("tipo")
                .GetInt32());

        Assert.Equal(
            valor,
            cupom.GetProperty("valor")
                .GetDecimal());

        Assert.Equal(
            50m,
            cupom.GetProperty(
                "valorMinimoProdutos")
                .GetDecimal());

        Assert.Equal(
            30m,
            cupom.GetProperty("descontoMaximo")
                .GetDecimal());

        Assert.Equal(
            100,
            cupom.GetProperty("limiteTotal")
                .GetInt32());

        Assert.Equal(
            2,
            cupom.GetProperty("limitePorCliente")
                .GetInt32());

        Assert.True(
            cupom.GetProperty("ativo")
                .GetBoolean());

        Assert.NotEqual(
            Guid.Empty,
            Versao(cupom));

        Assert.Equal(
            new DateTime(2026, 11, 1, 0, 0, 0),
            cupom.GetProperty("inicioLocal")
                .GetDateTime());

        // Confirma a listagem administrativa.
        using var listagem =
            await http.GetAsync(Rota);

        var json =
            await LerResposta(listagem);

        Assert.Equal(
            1,
            json.GetProperty("total")
                .GetInt32());

        Assert.Equal(
            1,
            json.GetProperty("totalAtivos")
                .GetInt32());

        Assert.Equal(
            0,
            json.GetProperty("totalInativos")
                .GetInt32());

        var registro = Assert.Single(
            json.GetProperty("cupons")
                .EnumerateArray()
                .ToList());

        Assert.Equal(
            id,
            registro.GetProperty("id")
                .GetInt32());

        // Confirma a conversão do horário de São Paulo para UTC no banco.
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistido =
            await db.Cupons.SingleAsync(
                c => c.Id == id);

        Assert.Equal(
            new DateTime(
                2026, 11, 1, 3, 0, 0,
                DateTimeKind.Utc),
            persistido.InicioEmUtc);

        Assert.Equal(
            new DateTime(
                2027, 1, 1, 2, 59, 59,
                DateTimeKind.Utc),
            persistido.FimEmUtc);

        Assert.Equal(
            valor,
            persistido.Valor);
    }

    // TESTE 4 — EDIÇÃO E CONTROLE DE VERSÃO

    [Fact]
    public async Task EdicaoNaoSobrescreveVersaoAntigaNemCodigo()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var id =
            await Cadastrar(http);

        var original =
            await Buscar(http, id);

        var versaoOriginal =
            Versao(original);

        var dados = Dados();

        dados["versao"] =
            versaoOriginal;

        dados["valor"] = 20m;

        dados["descricao"] =
            "Campanha atualizada";

        using var edicao =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await LerResposta(edicao);

        var atualizado =
            await Buscar(http, id);

        Assert.Equal(
            20m,
            atualizado.GetProperty("valor")
                .GetDecimal());

        Assert.Equal(
            "Campanha atualizada",
            atualizado.GetProperty("descricao")
                .GetString());

        Assert.NotEqual(
            versaoOriginal,
            Versao(atualizado));

        // Simula uma edição utilizando uma tela desatualizada.
        dados["valor"] = 90m;

        using var antiga =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await ConfirmarErro(antiga);

        // Mesmo com a versão atual, o código não pode ser modificado.
        dados["versao"] =
            Versao(atualizado);

        dados["codigo"] =
            "NOVOCODIGO";

        using var trocarCodigo =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await ConfirmarErro(trocarCodigo);

        var final =
            await Buscar(http, id);

        Assert.Equal(
            "KIORA15",
            final.GetProperty("codigo")
                .GetString());

        Assert.Equal(
            20m,
            final.GetProperty("valor")
                .GetDecimal());

        Assert.Equal(
            Versao(atualizado),
            Versao(final));
    }

    // TESTE 5 — ATIVAÇÃO E DESATIVAÇÃO

    [Fact]
    public async Task AlterarSituacaoPreservaConfiguracoes()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var id =
            await Cadastrar(http);

        var original =
            await Buscar(http, id);

        var versaoOriginal =
            Versao(original);

        using var desativar =
            await AlterarAtivo(
                http,
                id,
                versaoOriginal,
                false);

        await LerResposta(desativar);

        var inativo =
            await Buscar(http, id);

        Assert.False(
            inativo.GetProperty("ativo")
                .GetBoolean());

        Assert.NotEqual(
            versaoOriginal,
            Versao(inativo));

        // Versão antiga não pode reativar.
        using var antiga =
            await AlterarAtivo(
                http,
                id,
                versaoOriginal,
                true);

        await ConfirmarErro(antiga);

        // Repetir a situação atual com versão válida não altera o cupom.
        using var repetir =
            await AlterarAtivo(
                http,
                id,
                Versao(inativo),
                false);

        await LerResposta(repetir);

        var repetido =
            await Buscar(http, id);

        Assert.Equal(
            Versao(inativo),
            Versao(repetido));

        using var listagem =
            await http.GetAsync(Rota);

        var resumo =
            await LerResposta(listagem);

        Assert.Equal(
            0,
            resumo.GetProperty("totalAtivos")
                .GetInt32());

        Assert.Equal(
            1,
            resumo.GetProperty("totalInativos")
                .GetInt32());

        using var reativar =
            await AlterarAtivo(
                http,
                id,
                Versao(repetido),
                true);

        await LerResposta(reativar);

        var reativado =
            await Buscar(http, id);

        Assert.True(
            reativado.GetProperty("ativo")
                .GetBoolean());

        Assert.Equal(
            original.GetProperty("codigo")
                .GetString(),
            reativado.GetProperty("codigo")
                .GetString());

        Assert.Equal(
            original.GetProperty("valor")
                .GetDecimal(),
            reativado.GetProperty("valor")
                .GetDecimal());

        Assert.Equal(
            original.GetProperty("fimLocal")
                .GetDateTime(),
            reativado.GetProperty("fimLocal")
                .GetDateTime());

        Assert.Equal(
            original.GetProperty("parceiro")
                .GetString(),
            reativado.GetProperty("parceiro")
                .GetString());
    }

    // TESTE 6 — DUPLICIDADE DE CÓDIGO

    [Fact]
    public async Task CodigoDuplicadoEhRejeitadoMesmoInativo()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var id =
            await Cadastrar(http);

        using var duplicado =
            await http.PostAsJsonAsync(
                Rota,
                Dados("  kiora15  "));

        await ConfirmarErro(duplicado);

        var atual =
            await Buscar(http, id);

        using var desativar =
            await AlterarAtivo(
                http,
                id,
                Versao(atual),
                false);

        await LerResposta(desativar);

        using var duplicadoInativo =
            await http.PostAsJsonAsync(
                Rota,
                Dados("KIORA15"));

        await ConfirmarErro(
            duplicadoInativo);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Single(
            await db.Cupons.ToListAsync());
    }

    // TESTE 7 — VALIDAÇÕES COMERCIAIS

    [Theory]
    [InlineData("codigo", "AB")]
    [InlineData("codigo", "CÓDIGO")]
    [InlineData("valor", 101)]
    [InlineData("valor", 0)]
    [InlineData("valor", 1.234)]
    [InlineData("valorMinimoProdutos", -1)]
    [InlineData("descontoMaximo", 0)]
    [InlineData("limiteTotal", 0)]
    [InlineData("limitePorCliente", 0)]
    [InlineData("fimLocal", "2026-10-31T00:00:00")]
    [InlineData("inicioLocal", "2026-11-01T00:00:00Z")]
    [InlineData("tipo", 9)]
    public async Task DadosComerciaisInvalidosRetornam400(
        string campo,
        object valorInvalido)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var dados =
            Dados();

        dados[campo] =
            valorInvalido;

        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                dados);

        await ConfirmarErro(resposta);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Empty(
            await db.Cupons.ToListAsync());
    }

    // TESTE 8 — CUPOM INEXISTENTE

    [Fact]
    public async Task CupomInexistenteRetorna404()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        const int id =
            int.MaxValue;

        using var buscar =
            await http.GetAsync(
                $"{Rota}/{id}");

        await ConfirmarErro(
            buscar,
            HttpStatusCode.NotFound);

        var dados =
            Dados();

        dados["versao"] =
            Guid.NewGuid();

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await ConfirmarErro(
            editar,
            HttpStatusCode.NotFound);

        using var alterar =
            await AlterarAtivo(
                http,
                id,
                Guid.NewGuid(),
                false);

        await ConfirmarErro(
            alterar,
            HttpStatusCode.NotFound);

        using var relatorio =
            await http.GetAsync(
                $"{Rota}/{id}/relatorio");

        await ConfirmarErro(
            relatorio,
            HttpStatusCode.NotFound);
    }

    // TESTE 9 — HISTÓRICO E PAGINAÇÃO

    [Fact]
    public async Task RelatorioPreservaHistoricoELimitesDeUso()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var id =
            await Cadastrar(http);

        // Insere 51 utilizações fictícias.
        // 49 confirmadas
        // 1 reservada
        // 1 cancelada
        await InserirHistorico(host, id);

        var pagina1 =
            await Relatorio(http, id, 1);

        var resumo =
            pagina1.GetProperty("resumo");

        Assert.Equal(
            49,
            resumo.GetProperty("confirmados")
                .GetInt32());

        Assert.Equal(
            1,
            resumo.GetProperty("reservados")
                .GetInt32());

        Assert.Equal(
            1,
            resumo.GetProperty("cancelados")
                .GetInt32());

        Assert.Equal(
            50,
            resumo.GetProperty("utilizacoesAtivas")
                .GetInt32());

        Assert.Equal(
            51,
            resumo.GetProperty("totalRegistros")
                .GetInt32());

        // 49 utilizações confirmadas com R$ 5,00 de desconto cada.
        Assert.Equal(
            245m,
            resumo.GetProperty("descontosConfirmados")
                .GetDecimal());

        var historico1 =
            pagina1.GetProperty("historico");

        Assert.Equal(
            50,
            historico1.GetProperty("tamanhoPagina")
                .GetInt32());

        Assert.Equal(
            2,
            historico1.GetProperty("totalPaginas")
                .GetInt32());

        Assert.Equal(
            50,
            historico1.GetProperty("registros")
                .GetArrayLength());

        Assert.True(
            historico1.GetProperty("temProximaPagina")
                .GetBoolean());

        var pagina2 =
            await Relatorio(http, id, 2);

        var historico2 =
            pagina2.GetProperty("historico");

        var ultimo = Assert.Single(
            historico2.GetProperty("registros")
                .EnumerateArray()
                .ToList());

        Assert.True(
            historico2.GetProperty("temPaginaAnterior")
                .GetBoolean());

        Assert.False(
            historico2.GetProperty("temProximaPagina")
                .GetBoolean());

        var ultimoPedidoId =
            ultimo.GetProperty("pedidoId")
                .GetInt32();

        // Nenhum registro duplicado entre páginas consecutivas.
        Assert.DoesNotContain(
            historico1.GetProperty("registros")
                .EnumerateArray()
                .ToList(),

            uso => uso.GetProperty("pedidoId")
                .GetInt32() == ultimoPedidoId);

        // O serviço limita solicitações superiores à última página.
        var paginaInexistente =
            await Relatorio(http, id, 999);

        Assert.Equal(
            2,
            paginaInexistente
                .GetProperty("historico")
                .GetProperty("pagina")
                .GetInt32());

        // Não pode reduzir o limite total abaixo dos 50 usos ativos.
        var atual =
            await Buscar(http, id);

        var dados =
            Dados();

        dados["versao"] =
            Versao(atual);

        dados["limiteTotal"] = 49;

        using var reduzirLimite =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await ConfirmarErro(
            reduzirLimite);

        // Altera a campanha sem modificar os descontos das compras anteriores.
        atual =
            await Buscar(http, id);

        dados["versao"] =
            Versao(atual);

        dados["limiteTotal"] = 100;

        dados["valor"] = 25m;

        dados["parceiro"] =
            "Novo parceiro";

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                dados);

        await LerResposta(editar);

        var depois =
            await Relatorio(http, id);

        Assert.Equal(
            245m,
            depois.GetProperty("resumo")
                .GetProperty("descontosConfirmados")
                .GetDecimal());

        Assert.Equal(
            "Novo parceiro",
            depois.GetProperty("cupom")
                .GetProperty("parceiro")
                .GetString());

        // Cada compra preserva o parceiro e desconto registrados originalmente.
        Assert.All(
            depois.GetProperty("historico")
                .GetProperty("registros")
                .EnumerateArray()
                .ToList(),

            uso =>
            {
                Assert.Equal(
                    "Parceiro original",
                    uso.GetProperty("parceiro")
                        .GetString());

                Assert.Equal(
                    5m,
                    uso.GetProperty("desconto")
                        .GetDecimal());
            });

        var editado =
            await Buscar(http, id);

        using var desativar =
            await AlterarAtivo(
                http,
                id,
                Versao(editado),
                false);

        await LerResposta(desativar);

        var inativo =
            await Relatorio(http, id);

        // Desativar não elimina o histórico.
        Assert.Equal(
            51,
            inativo.GetProperty("resumo")
                .GetProperty("totalRegistros")
                .GetInt32());

        Assert.Equal(
            245m,
            inativo.GetProperty("resumo")
                .GetProperty("descontosConfirmados")
                .GetDecimal());

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var pedidos =
            await db.Pedidos
                .Where(p =>
                    p.CodigoCupom == "KIORA15")
                .ToListAsync();

        Assert.Equal(
            51,
            pedidos.Count);

        Assert.All(
            pedidos,

            pedido =>
            {
                Assert.Equal(
                    5m,
                    pedido.DescontoCupom);

                Assert.Equal(
                    45m,
                    pedido.ValorTotal);
            });
    }

    // TESTE 10 — REVOGAÇÃO DA AUTENTICAÇÃO

    [Fact]
    public async Task AdministradorDesativadoPerdeAcesso()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var id =
            await Cadastrar(http);

        var cupom =
            await Buscar(http, id);

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

        using var listar =
            await http.GetAsync(Rota);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            listar.StatusCode);

        using var alterar =
            await AlterarAtivo(
                http,
                id,
                Versao(cupom),
                false);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            alterar.StatusCode);
    }

    // DADOS FICTÍCIOS DO HISTÓRICO
    private static async Task InserirHistorico(
        HostMobile host,
        int cupomId)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var cliente =
            await db.Usuarios.SingleAsync(
                u => u.Email ==
                    "cliente@mobile.test");

        var cupom =
            await db.Cupons.SingleAsync(
                c => c.Id == cupomId);

        var data = new DateTime(
            2026, 11, 17, 12, 0, 0,
            DateTimeKind.Utc);

        for (int i = 0; i < 51; i++)
        {
            var cancelado = i == 50;

            var reservado = i == 49;

            var pedido = new Pedido
            {
                UsuarioId = cliente.Id,

                DataPedido =
                    data.AddMinutes(i),

                FormaPagamento =
                    FormaPagamento.MercadoPago,

                StatusPedido = cancelado
                    ? StatusPedido.Cancelado
                    : StatusPedido.Recebido,

                StatusPagamento = reservado
                    ? StatusPagamento.Pendente
                    : StatusPagamento.Aprovado,

                CodigoCupom =
                    cupom.Codigo,

                DescontoCupom = 5m,

                ValorTotal = 45m,

                TaxaEntrega = 0m,

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
                    }
            };

            var uso = new UsoCupom
            {
                CupomId = cupomId,

                Pedido = pedido,

                UsuarioId = cliente.Id,

                Codigo = cupom.Codigo,

                Parceiro =
                    "Parceiro original",

                Desconto = 5m,

                // O serviço reconciliará a situação conforme o pedido.
                Situacao =
                    SituacaoUsoCupom.Reservado,

                ReservadoEmUtc =
                    data.AddMinutes(i)
            };

            db.Pedidos.Add(pedido);

            db.UsosCupom.Add(uso);
        }

        cupom.UtilizacoesAtivas = 51;

        await db.SaveChangesAsync();
    }
}
