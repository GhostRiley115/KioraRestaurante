using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

public sealed class RecuperacaoSenhaApiHttpTests
{
    private const string Rota =
        "/api/usuarios/recuperacao";

    private const string Email =
        "cliente@mobile.test";

    private const string EmailAusente =
        "inexistente@mobile.test";

    private const string NovaSenha =
        "NovaSenhaTeste2026!";

    // ==================================================
    // MÉTODOS AUXILIARES
    // ==================================================

    private static Task<HttpResponseMessage> Solicitar(
        HttpClient http,
        string email = Email)
    {
        return http.PostAsJsonAsync(
            $"{Rota}/solicitar",
            new { email });
    }

    private static Task<HttpResponseMessage> Validar(
        HttpClient http,
        string codigo)
    {
        return http.PostAsJsonAsync(
            $"{Rota}/validar",
            new
            {
                email = Email,
                codigo
            });
    }

    private static Task<HttpResponseMessage> Redefinir(
        HttpClient http,
        string codigo,
        string senha = NovaSenha,
        string? confirmacao = null)
    {
        return http.PostAsJsonAsync(
            $"{Rota}/redefinir",
            new
            {
                email = Email,
                codigo,
                novaSenha = senha,
                confirmarNovaSenha =
                    confirmacao ?? senha
            });
    }

    private static Task<HttpResponseMessage> Login(
        HttpClient http,
        string senha)
    {
        return http.PostAsJsonAsync(
            "/api/usuarios/login",
            new
            {
                email = Email,
                senha
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
    // TESTE 1 — RECUPERAÇÃO COMPLETA E REVOGAÇÃO DO JWT
    // ==================================================

    [Fact]
    public async Task RecuperacaoCompletaRedefineSenhaERevogaJwt()
    {
        var email = new EmailServiceFalso();

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        // Realiza o login com a senha original.
        using var loginAnterior =
            await Login(http, HostMobile.Senha);

        var jsonLogin =
            await ConfirmarSucesso(loginAnterior);

        var tokenAnterior = jsonLogin
            .GetProperty("token")
            .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(tokenAnterior));

        // Solicita o código de recuperação.
        using var solicitacao =
            await Solicitar(http);

        await ConfirmarSucesso(solicitacao);

        var envio = Assert.Single(email.Envios);

        Assert.Equal(
            Email,
            envio.Email);

        Assert.Matches(
            @"^[0-9]{6}$",
            envio.Codigo);

        // Confere que a resposta não revela
        // se determinado e-mail está cadastrado.
        using var ausente =
            await Solicitar(http, EmailAusente);

        await ConfirmarSucesso(ausente);

        Assert.Equal(
            await solicitacao.Content.ReadAsStringAsync(),
            await ausente.Content.ReadAsStringAsync());

        // O código não pode aparecer na resposta JSON.
        Assert.DoesNotContain(
            envio.Codigo,
            await solicitacao.Content.ReadAsStringAsync());

        Assert.Single(email.Envios);

        // Valida o código recebido.
        using var validacao =
            await Validar(http, envio.Codigo);

        await ConfirmarSucesso(validacao);

        // Redefine a senha.
        using var redefinicao =
            await Redefinir(http, envio.Codigo);

        await ConfirmarSucesso(redefinicao);

        // Impede a reutilização do mesmo código.
        using var reutilizacao =
            await Redefinir(
                http,
                envio.Codigo,
                "OutraSenha2026!");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            reutilizacao.StatusCode);

        // A senha anterior não pode mais funcionar.
        using var senhaAntiga =
            await Login(http, HostMobile.Senha);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            senhaAntiga.StatusCode);

        // A nova senha deve funcionar.
        using var novoLogin =
            await Login(http, NovaSenha);

        await ConfirmarSucesso(novoLogin);

        // O JWT emitido antes da alteração
        // deve estar revogado.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenAnterior);

        using var perfil =
            await http.GetAsync(
                "/api/usuarios/perfil");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            perfil.StatusCode);
    }

    // ==================================================
    // TESTE 2 — DADOS INVÁLIDOS
    // ==================================================

    [Fact]
    public async Task DadosInvalidosNaoPermitemRedefinirSenha()
    {
        var email = new EmailServiceFalso();

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        // E-mail inválido.
        using var emailInvalido =
            await Solicitar(
                http,
                "email-invalido");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            emailInvalido.StatusCode);

        Assert.Empty(email.Envios);

        // Solicitação válida.
        using var solicitacao =
            await Solicitar(http);

        await ConfirmarSucesso(solicitacao);

        var envio = Assert.Single(email.Envios);

        // Garante que o código incorreto seja
        // diferente do código real gerado.
        var incorreto =
            envio.Codigo == "000000"
                ? "111111"
                : "000000";

        // Código incorreto.
        using var validacao =
            await Validar(http, incorreto);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            validacao.StatusCode);

        // Senha fora dos requisitos.
        using var senhaFraca =
            await Redefinir(
                http,
                envio.Codigo,
                "123");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            senhaFraca.StatusCode);

        // Confirmação de senha divergente.
        using var divergencia =
            await Redefinir(
                http,
                envio.Codigo,
                NovaSenha,
                "Diferente123!");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            divergencia.StatusCode);

        // Tentativa de redefinir com código incorreto.
        using var codigoErrado =
            await Redefinir(http, incorreto);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            codigoErrado.StatusCode);

        // A senha original deve permanecer válida.
        using var login =
            await Login(http, HostMobile.Senha);

        await ConfirmarSucesso(login);
    }

    // ==================================================
    // TESTE 3 — CÓDIGO EXPIRADO
    // ==================================================

    [Fact]
    public async Task CodigoExpiradoNaoPermiteRecuperacao()
    {
        var email = new EmailServiceFalso();

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        using var solicitacao =
            await Solicitar(http);

        await ConfirmarSucesso(solicitacao);

        var envio = Assert.Single(email.Envios);

        // Simula a expiração do código
        // exclusivamente no banco temporário.
        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var banco = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var alterados = await banco.Usuarios
                .Where(u => u.Email == Email)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(
                        u => u.ExpiracaoTokenRecuperacaoSenha,
                        DateTime.UtcNow.AddMinutes(-1)));

            Assert.Equal(1, alterados);
        }

        using var validacao =
            await Validar(http, envio.Codigo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            validacao.StatusCode);

        using var redefinicao =
            await Redefinir(http, envio.Codigo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            redefinicao.StatusCode);
    }

    // ==================================================
    // TESTE 4 — LIMITE DE SOLICITAÇÕES
    // ==================================================

    [Fact]
    public async Task QuartaSolicitacaoDeCodigoRetorna429()
    {
        var email = new EmailServiceFalso();

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        // As três primeiras solicitações
        // devem ser autorizadas.
        for (var i = 0; i < 3; i++)
        {
            using var resposta =
                await Solicitar(http);

            await ConfirmarSucesso(resposta);
        }

        // A quarta solicitação deve ser bloqueada.
        using var bloqueada =
            await Solicitar(http);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            bloqueada.StatusCode);

        Assert.NotNull(
            bloqueada.Headers.RetryAfter);

        Assert.Equal(
            3,
            email.Envios.Count);
    }

    // ==================================================
    // TESTE 5 — LIMITE COMPARTILHADO DE TENTATIVAS
    // ==================================================

    [Fact]
    public async Task ValidacaoERedefinicaoCompartilhamLimite()
    {
        var email = new EmailServiceFalso();

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        // Alterna entre validação e redefinição
        // para testar o limite compartilhado.
        for (var i = 0; i < 10; i++)
        {
            using var resposta =
                i % 2 == 0
                    ? await Validar(http, "000000")
                    : await Redefinir(http, "000000");

            Assert.Equal(
                HttpStatusCode.BadRequest,
                resposta.StatusCode);
        }

        // A décima primeira tentativa
        // precisa ser bloqueada.
        using var bloqueada =
            await Validar(http, "000000");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            bloqueada.StatusCode);

        Assert.NotNull(
            bloqueada.Headers.RetryAfter);

        Assert.Empty(email.Envios);
    }

    // ==================================================
    // TESTE 6 — FALHA NO ENVIO DE E-MAIL
    // ==================================================

    [Fact]
    public async Task FalhaNoEnvioInvalidaCodigoSemRevelarConta()
    {
        // Simula uma falha no serviço de e-mail.
        var email = new EmailServiceFalso(
            sucesso: false);

        await using var host =
            await HostMobile.Criar(email);

        using var http =
            host.Cliente(cookies: false);

        using var solicitacao =
            await Solicitar(http);

        await ConfirmarSucesso(solicitacao);

        var envio = Assert.Single(email.Envios);

        // Verifica que a resposta continua genérica.
        using var ausente =
            await Solicitar(http, EmailAusente);

        await ConfirmarSucesso(ausente);

        Assert.Equal(
            await solicitacao.Content.ReadAsStringAsync(),
            await ausente.Content.ReadAsStringAsync());

        Assert.Single(email.Envios);

        // O código cujo envio falhou
        // não pode ser considerado válido.
        using var validacao =
            await Validar(http, envio.Codigo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            validacao.StatusCode);

        using var redefinicao =
            await Redefinir(http, envio.Codigo);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            redefinicao.StatusCode);
    }
}
