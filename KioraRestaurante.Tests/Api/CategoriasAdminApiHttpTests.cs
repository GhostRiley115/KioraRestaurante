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

// Testes HTTP do gerenciamento administrativo de categorias do Kiora Mobile.
// Verifica segurança, cadastro, edição, duplicidade, desativação, reativação e integração com o cardápio público.
public sealed class CategoriasAdminApiHttpTests
{
    private const string Rota =
        "/api/admin/categorias";

    private const string RotaPublica =
        "/api/cardapio/categorias";

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
    private static async Task<JsonElement> LerResposta(
        HttpResponseMessage resposta,
        HttpStatusCode esperado = HttpStatusCode.OK)
    {
        Assert.Equal(
            esperado,
            resposta.StatusCode);

        var json = await resposta.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.True(
            json.GetProperty("sucesso")
                .GetBoolean());

        return json;
    }
    private static async Task<JsonElement> Cadastrar(
        HttpClient http,
        string nome)
    {
        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                new { nome });

        var json = await LerResposta(
            resposta,
            HttpStatusCode.Created);

        Assert.NotNull(
            resposta.Headers.Location);

        var categoria =
            json.GetProperty("categoria");

        var id = categoria
            .GetProperty("id")
            .GetInt32();

        Assert.EndsWith(
            $"{Rota}/{id}",
            resposta.Headers.Location!.ToString());

        return categoria;
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

        return json.GetProperty("categoria");
    }
    private static async Task<JsonElement> AlterarAtiva(
        HttpClient http,
        int id,
        bool ativa)
    {
        using var resposta =
            await http.PatchAsJsonAsync(
                $"{Rota}/{id}/ativa",
                new { ativa });

        var json =
            await LerResposta(resposta);

        return json.GetProperty("categoria");
    }

    private static async Task<List<JsonElement>>
        CategoriasPublicas(HttpClient http)
    {
        using var resposta =
            await http.GetAsync(RotaPublica);

        Assert.Equal(
            HttpStatusCode.OK,
            resposta.StatusCode);

        var json = await resposta.Content
            .ReadFromJsonAsync<JsonElement>();

        return json
            .EnumerateArray()
            .ToList();
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

    // TESTE 1 — AUTENTICAÇÃO OBRIGATÓRIA

    [Fact]
    public async Task CategoriasAdminExigemAutenticacao()
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
                new
                {
                    nome = "Categoria não autorizada"
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            cadastrar.StatusCode);

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/1",
                new
                {
                    nome = "Alteração não autorizada"
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            editar.StatusCode);

        using var desativar =
            await http.PatchAsJsonAsync(
                $"{Rota}/1/ativa",
                new
                {
                    ativa = false
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            desativar.StatusCode);

        // O cardápio público deve continuar acessível sem autenticação.
        var publicas =
            await CategoriasPublicas(http);

        Assert.Equal(
            4,
            publicas.Count);
    }

    // TESTE 2 — PERMISSÕES POR CARGO

    [Theory]
    [InlineData(TipoUsuario.Cliente)]
    [InlineData(TipoUsuario.Gerente)]
    [InlineData(TipoUsuario.Garcom)]
    [InlineData(TipoUsuario.Portaria)]
    [InlineData(TipoUsuario.Cozinha)]
    public async Task OutrosCargosNaoGerenciamCategorias(
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
                new
                {
                    nome = "Categoria proibida"
                });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            cadastrar.StatusCode);

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/1",
                new
                {
                    nome = "Nome proibido"
                });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            editar.StatusCode);

        using var alterar =
            await http.PatchAsJsonAsync(
                $"{Rota}/1/ativa",
                new
                {
                    ativa = false
                });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            alterar.StatusCode);

        // Confirma que nenhuma alteração indevida foi persistida.
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Equal(
            4,
            await db.Categorias.CountAsync());

        var categoria =
            await db.Categorias
                .SingleAsync(c => c.Id == 1);

        Assert.Equal(
            "Entradas",
            categoria.Nome);

        Assert.True(
            categoria.Ativa);
    }

    // TESTE 3 — LISTAGEM ADMINISTRATIVA

    [Fact]
    public async Task ListagemRetornaCategoriasEProdutosVinculados()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.GetAsync(Rota);

        var json =
            await LerResposta(resposta);

        Assert.Equal(
            4,
            json.GetProperty("total")
                .GetInt32());

        Assert.Equal(
            4,
            json.GetProperty("totalAtivas")
                .GetInt32());

        Assert.Equal(
            0,
            json.GetProperty("totalInativas")
                .GetInt32());

        var categorias =
            json.GetProperty("categorias")
                .EnumerateArray()
                .ToList();

        var principais =
            categorias.Single(c =>
                c.GetProperty("id")
                    .GetInt32() == 2);

        Assert.Equal(
            "Pratos Principais",
            principais.GetProperty("nome")
                .GetString());

        // O HostMobile possui um produto cadastrado na categoria 2.
        Assert.Equal(
            1,
            principais.GetProperty("totalProdutos")
                .GetInt32());

        Assert.Equal(
            1,
            principais.GetProperty("produtosAtivos")
                .GetInt32());

        Assert.False(
            principais.GetProperty("podeDesativar")
                .GetBoolean());

        var entradas =
            categorias.Single(c =>
                c.GetProperty("id")
                    .GetInt32() == 1);

        Assert.Equal(
            0,
            entradas.GetProperty("totalProdutos")
                .GetInt32());

        Assert.True(
            entradas.GetProperty("podeDesativar")
                .GetBoolean());
    }

    // TESTE 4 — CADASTRO DE CATEGORIA

    [Fact]
    public async Task CadastroRetorna201EPreservaNovaCategoria()
    {
        await using var host =
            await HostMobile.Criar();

        using var admin =
            host.Cliente(cookies: false);

        using var publico =
            host.Cliente(cookies: false);

        Autenticar(admin, host);

        var categoria = await Cadastrar(
            admin,
            "  Combinados  ");

        var id =
            categoria.GetProperty("id")
                .GetInt32();

        Assert.True(id > 4);

        Assert.Equal(
            "Combinados",
            categoria.GetProperty("nome")
                .GetString());

        Assert.True(
            categoria.GetProperty("ativa")
                .GetBoolean());

        Assert.Equal(
            0,
            categoria.GetProperty("totalProdutos")
                .GetInt32());

        Assert.True(
            categoria.GetProperty("podeDesativar")
                .GetBoolean());

        // Consulta administrativa.
        var detalhes =
            await Buscar(admin, id);

        Assert.Equal(
            id,
            detalhes.GetProperty("id")
                .GetInt32());

        // A nova categoria também deverá aparecer no cardápio público.
        var publicas =
            await CategoriasPublicas(publico);

        Assert.Contains(
            publicas,
            c => c.GetProperty("id")
                .GetInt32() == id);

        // Confirma a persistência no banco.
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var persistida =
            await db.Categorias
                .SingleAsync(c => c.Id == id);

        Assert.Equal(
            "Combinados",
            persistida.Nome);

        Assert.True(
            persistida.Ativa);
    }

    // TESTE 5 — EDIÇÃO DE CATEGORIA

    [Fact]
    public async Task EditarCategoriaPreservaProdutosVinculados()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.PutAsJsonAsync(
                $"{Rota}/2",
                new
                {
                    nome =
                        "Pratos Orientais"
                });

        var json =
            await LerResposta(resposta);

        var categoria =
            json.GetProperty("categoria");

        Assert.Equal(
            2,
            categoria.GetProperty("id")
                .GetInt32());

        Assert.Equal(
            "Pratos Orientais",
            categoria.GetProperty("nome")
                .GetString());

        Assert.Equal(
            1,
            categoria.GetProperty("totalProdutos")
                .GetInt32());

        Assert.True(
            categoria.GetProperty("ativa")
                .GetBoolean());

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var produto =
            await db.Produtos
                .Include(p => p.Categoria)
                .SingleAsync();

        Assert.Equal(
            2,
            produto.CategoriaId);

        Assert.Equal(
            "Pratos Orientais",
            produto.Categoria.Nome);

        Assert.True(produto.Ativo);
    }

    // TESTE 6 — DUPLICIDADE DE CADASTRO

    [Theory]
    [InlineData("Entradas")]
    [InlineData("entradas")]
    [InlineData("  ENTRADAS  ")]
    public async Task CadastroNaoPermiteNomeDuplicado(
        string nome)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                new { nome });

        await ConfirmarErro(resposta);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Equal(
            4,
            await db.Categorias.CountAsync());
    }

    // TESTE 7 — DUPLICIDADE NA EDIÇÃO

    [Fact]
    public async Task EdicaoNaoPermiteNomeDeOutraCategoria()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.PutAsJsonAsync(
                $"{Rota}/2",
                new
                {
                    nome = "BEBIDAS"
                });

        await ConfirmarErro(resposta);

        var categoria =
            await Buscar(http, 2);

        Assert.Equal(
            "Pratos Principais",
            categoria.GetProperty("nome")
                .GetString());

        // Editar mantendo o próprio nome não constitui duplicidade.
        using var manterNome =
            await http.PutAsJsonAsync(
                $"{Rota}/2",
                new
                {
                    nome = "Pratos Principais"
                });

        await LerResposta(manterNome);
    }

    // TESTE 8 — DESATIVAÇÃO E REATIVAÇÃO

    [Fact]
    public async Task CategoriaInativaNaoApareceNoCardapio()
    {
        await using var host =
            await HostMobile.Criar();

        using var admin =
            host.Cliente(cookies: false);

        using var publico =
            host.Cliente(cookies: false);

        Autenticar(admin, host);

        var categoria = await Cadastrar(
            admin,
            "Especiais");

        var id =
            categoria.GetProperty("id")
                .GetInt32();

        // Desativa a categoria.
        var inativa = await AlterarAtiva(
            admin,
            id,
            false);

        Assert.False(
            inativa.GetProperty("ativa")
                .GetBoolean());

        Assert.False(
            inativa.GetProperty("podeDesativar")
                .GetBoolean());

        Assert.True(
            inativa.GetProperty("podeReativar")
                .GetBoolean());

        var publicas =
            await CategoriasPublicas(publico);

        Assert.DoesNotContain(
            publicas,
            c => c.GetProperty("id")
                .GetInt32() == id);

        using var produtosCategoriaInativa =
            await publico.GetAsync(
                "/api/cardapio/produtos" +
                $"?categoriaId={id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            produtosCategoriaInativa.StatusCode);

        // Confirma a listagem de inativas.
        using var listarInativas =
            await admin.GetAsync(
                Rota + "?ativa=false");

        var json =
            await LerResposta(listarInativas);

        Assert.Equal(
            1,
            json.GetProperty("total")
                .GetInt32());

        var registro = Assert.Single(
            json.GetProperty("categorias")
                .EnumerateArray()
                .ToList());

        Assert.Equal(
            id,
            registro.GetProperty("id")
                .GetInt32());

        // Confirma a listagem de ativas.
        using var listarAtivas =
            await admin.GetAsync(
                Rota + "?ativa=true");

        var jsonAtivas =
            await LerResposta(listarAtivas);

        Assert.Equal(
            4,
            jsonAtivas.GetProperty("total")
                .GetInt32());

        Assert.All(
            jsonAtivas.GetProperty("categorias")
                .EnumerateArray()
                .ToList(),
            item => Assert.True(
                item.GetProperty("ativa")
                    .GetBoolean()));

        // Reativa a mesma categoria.
        var reativada = await AlterarAtiva(
            admin,
            id,
            true);

        Assert.True(
            reativada.GetProperty("ativa")
                .GetBoolean());

        Assert.Equal(
            id,
            reativada.GetProperty("id")
                .GetInt32());

        publicas =
            await CategoriasPublicas(publico);

        Assert.Contains(
            publicas,
            c => c.GetProperty("id")
                .GetInt32() == id);

        Assert.Equal(
            5,
            publicas.Count);
    }

    // TESTE 9 — DUPLICIDADE COM CATEGORIA INATIVA

    [Fact]
    public async Task CategoriaInativaContinuaReservandoNome()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var categoria = await Cadastrar(
            http,
            "Combinados");

        var id =
            categoria.GetProperty("id")
                .GetInt32();

        await AlterarAtiva(
            http,
            id,
            false);

        using var duplicada =
            await http.PostAsJsonAsync(
                Rota,
                new
                {
                    nome = "combinados"
                });

        await ConfirmarErro(duplicada);

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/1",
                new
                {
                    nome = "COMBINADOS"
                });

        await ConfirmarErro(editar);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Equal(
            5,
            await db.Categorias.CountAsync());
    }

    // TESTE 10 — PROTEÇÃO DOS PRODUTOS ATIVOS

    [Fact]
    public async Task CategoriaComProdutosAtivosNaoPodeSerDesativada()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        // A categoria 2 possui o produto criado pelo HostMobile.
        using var resposta =
            await http.PatchAsJsonAsync(
                $"{Rota}/2/ativa",
                new
                {
                    ativa = false
                });

        await ConfirmarErro(resposta);

        var categoria =
            await Buscar(http, 2);

        Assert.True(
            categoria.GetProperty("ativa")
                .GetBoolean());

        Assert.Equal(
            1,
            categoria.GetProperty("produtosAtivos")
                .GetInt32());

        Assert.False(
            categoria.GetProperty("podeDesativar")
                .GetBoolean());

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var produto =
            await db.Produtos.SingleAsync();

        Assert.True(produto.Ativo);

        Assert.Equal(
            2,
            produto.CategoriaId);
    }

    // TESTE 11 — INTEGRAÇÃO COM PRODUTOS

    [Fact]
    public async Task ProdutoSoPodeReativarComCategoriaAtiva()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        int produtoId;

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            produtoId =
                await db.Produtos
                    .Select(p => p.Id)
                    .SingleAsync();
        }

        var rotaProduto =
            $"/api/admin/produtos/{produtoId}/ativo";

        // Primeiro desativa o produto.
        using var desativarProduto =
            await http.PatchAsJsonAsync(
                rotaProduto,
                new
                {
                    ativo = false
                });

        await LerResposta(desativarProduto);

        // Agora permite desativar a categoria, mantendo seu relacionamento histórico.
        var inativa = await AlterarAtiva(
            http,
            2,
            false);

        Assert.False(
            inativa.GetProperty("ativa")
                .GetBoolean());

        Assert.Equal(
            1,
            inativa.GetProperty("totalProdutos")
                .GetInt32());

        Assert.Equal(
            0,
            inativa.GetProperty("produtosAtivos")
                .GetInt32());

        Assert.Equal(
            1,
            inativa.GetProperty("produtosInativos")
                .GetInt32());

        // Produto não pode ser reativado enquanto sua categoria está inativa.
        using var reativacaoProibida =
            await http.PatchAsJsonAsync(
                rotaProduto,
                new
                {
                    ativo = true
                });

        await ConfirmarErro(
            reativacaoProibida);

        // Reativa a categoria primeiro.
        await AlterarAtiva(
            http,
            2,
            true);

        // Agora permite reativar o produto.
        using var reativarProduto =
            await http.PatchAsJsonAsync(
                rotaProduto,
                new
                {
                    ativo = true
                });

        await LerResposta(reativarProduto);

        await using var scopeFinal =
            host.App.Services.CreateAsyncScope();

        var dbFinal =
            scopeFinal.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var produtoFinal =
            await dbFinal.Produtos
                .Include(p => p.Categoria)
                .SingleAsync(p =>
                    p.Id == produtoId);

        Assert.True(
            produtoFinal.Ativo);

        Assert.True(
            produtoFinal.Categoria.Ativa);

        Assert.Equal(
            2,
            produtoFinal.CategoriaId);

        // Reativar não disponibiliza automaticamente o produto.
        Assert.False(
            produtoFinal.Disponivel);
    }

    // TESTE 12 — CATEGORIA INEXISTENTE

    [Fact]
    public async Task CategoriaInexistenteRetorna404()
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

        using var editar =
            await http.PutAsJsonAsync(
                $"{Rota}/{id}",
                new
                {
                    nome = "Categoria inexistente"
                });

        await ConfirmarErro(
            editar,
            HttpStatusCode.NotFound);

        using var alterar =
            await http.PatchAsJsonAsync(
                $"{Rota}/{id}/ativa",
                new
                {
                    ativa = false
                });

        await ConfirmarErro(
            alterar,
            HttpStatusCode.NotFound);
    }

    // TESTE 13 — NOMES INVÁLIDOS

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Nome\nInvalido")]
    [InlineData(null)]
    public async Task CadastroRejeitaNomeInvalido(
        string? nome)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                new { nome });

        await ConfirmarErro(resposta);
    }

    // TESTE 14 — TAMANHO MÁXIMO DO NOME

    [Fact]
    public async Task CategoriaRespeitaLimiteDe50Caracteres()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        var nomeValido =
            new string('A', 50);

        var categoria =
            await Cadastrar(
                http,
                nomeValido);

        Assert.Equal(
            nomeValido,
            categoria.GetProperty("nome")
                .GetString());

        using var resposta =
            await http.PostAsJsonAsync(
                Rota,
                new
                {
                    nome = new string('B', 51)
                });

        await ConfirmarErro(resposta);
    }

    // TESTE 15 — SITUAÇÃO OBRIGATÓRIA

    [Fact]
    public async Task AlteracaoDeSituacaoExigeCampoAtiva()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await http.PatchAsJsonAsync(
                $"{Rota}/1/ativa",
                new { });

        await ConfirmarErro(resposta);

        var categoria =
            await Buscar(http, 1);

        Assert.True(
            categoria.GetProperty("ativa")
                .GetBoolean());
    }

    // TESTE 16 — REVOGAÇÃO DA AUTENTICAÇÃO

    [Fact]
    public async Task AdministradorDesativadoPerdeAcesso()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var inicial =
            await http.GetAsync(Rota);

        await LerResposta(inicial);

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

        using var cadastrar =
            await http.PostAsJsonAsync(
                Rota,
                new
                {
                    nome = "Cadastro não autorizado"
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            cadastrar.StatusCode);
    }
}