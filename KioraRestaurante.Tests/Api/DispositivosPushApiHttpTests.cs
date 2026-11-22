using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

// Testes HTTP de segurança dos dispositivos utilizados pelas notificações push do Kiora.
// Utiliza SQLite temporário e tokens fictícios.
// Não realiza nenhuma comunicação com Firebase.
public sealed class DispositivosPushApiHttpTests
{
    private const string Rota =
        "/api/reservas/dispositivos";

    private const string TokenFicticio =
        "TOKEN_FCM_FICTICIO_TESTE_KIORA_123456789";

    // MÉTODOS AUXILIARES
    private static void Autenticar(
        HttpClient http,
        HostMobile host,
        bool outroCliente = false)
    {
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                host.Token(
                    TipoUsuario.Cliente,
                    outroCliente));
    }
    private static async Task AbrirSessaoVisitante(
        HttpClient http,
        bool incluirCsrf = true)
    {
        var sessao =
            await http.GetFromJsonAsync<JsonElement>(
                "/api/reservas/sessao");

        http.DefaultRequestHeaders.Remove(
            "X-Reserva-Chave");

        http.DefaultRequestHeaders.Add(
            "X-Reserva-Chave",
            sessao.GetProperty("chaveVisitante")
                .GetString());

        if (incluirCsrf)
        {
            http.DefaultRequestHeaders.Remove(
                "X-CSRF-TOKEN");

            http.DefaultRequestHeaders.Add(
                "X-CSRF-TOKEN",
                sessao.GetProperty("csrfToken")
                    .GetString());
        }
    }

    private static Task<HttpResponseMessage>
        Registrar(
            HttpClient http,
            string token = TokenFicticio)
    {
        return http.PostAsJsonAsync(
            Rota,
            new { token });
    }

    private static Task<HttpResponseMessage>
        Desativar(
            HttpClient http,
            string token = TokenFicticio)
    {
        return http.PostAsJsonAsync(
            Rota + "/desativar",
            new { token });
    }
    private static async Task ConfirmarSucesso(
        HttpResponseMessage resposta)
    {
        var corpo =
            await resposta.Content.ReadAsStringAsync();

        Assert.True(
            resposta.StatusCode == HttpStatusCode.OK,

            $"HTTP recebido: " +
            $"{(int)resposta.StatusCode}. " +
            $"Resposta: {corpo}");

        using var documento =
            JsonDocument.Parse(corpo);

        Assert.True(
            documento.RootElement
                .GetProperty("sucesso")
                .GetBoolean());

        // O token não deve ser revelado.
        Assert.DoesNotContain(
            TokenFicticio,
            corpo);
    }

    private static async Task<DispositivoPush>
        LerDispositivo(HostMobile host)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await db.DispositivosPush
            .AsNoTracking()
            .SingleAsync();
    }

    private static async Task<int>
        QuantidadeDispositivos(HostMobile host)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await db.DispositivosPush
            .CountAsync();
    }

    // TESTE 1 — PROTEÇÃO CSRF E BEARER INVÁLIDO

    [Fact]
    public async Task VisitantePrecisaProtecaoAntifalsificacao()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente();

        await AbrirSessaoVisitante(
            http,
            incluirCsrf: false);

        // Visitante sem CSRF não pode registrar.
        using var semCsrf =
            await Registrar(http);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            semCsrf.StatusCode);

        // Um Bearer falso também não dispensa a proteção antifalsificação.
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "token-invalido");

        using var bearerInvalido =
            await Registrar(http);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            bearerInvalido.StatusCode);

        Assert.Equal(
            0,
            await QuantidadeDispositivos(host));

        http.DefaultRequestHeaders.Authorization =
            null;

        // Abre a sessão com a proteção correta.
        await AbrirSessaoVisitante(http);

        using var resposta =
            await Registrar(http);

        await ConfirmarSucesso(resposta);

        var dispositivo =
            await LerDispositivo(host);

        Assert.True(dispositivo.Ativo);

        Assert.Null(
            dispositivo.UsuarioId);

        Assert.Null(
            dispositivo.VersaoSessaoUsuario);

        Assert.False(
            string.IsNullOrEmpty(
                dispositivo.VisitanteHash));
    }

    // TESTE 2 — JWT, CRIPTOGRAFIA E NOVO REGISTRO

    [Fact]
    public async Task RegistroProtegeTokenEIdentificaSessao()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await Registrar(http);

        await ConfirmarSucesso(resposta);

        var dispositivo =
            await LerDispositivo(host);

        Assert.True(dispositivo.Ativo);

        Assert.NotNull(
            dispositivo.UsuarioId);

        Assert.NotNull(
            dispositivo.VersaoSessaoUsuario);

        Assert.NotEqual(
            Guid.Empty,
            dispositivo.Versao);

        // O token precisa estar protegido.
        Assert.NotEqual(
            TokenFicticio,
            dispositivo.TokenFcm);

        Assert.DoesNotContain(
            TokenFicticio,
            dispositivo.TokenFcm);

        var hashEsperado =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        TokenFicticio)));

        Assert.Equal(
            hashEsperado,
            dispositivo.TokenHash);

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var usuario =
                await db.Usuarios.SingleAsync(
                    u => u.Email ==
                        "cliente@mobile.test");

            Assert.Equal<int?>(
                usuario.Id,
                dispositivo.UsuarioId);

            Assert.Equal<Guid?>(
                usuario.VersaoSessao,
                dispositivo.VersaoSessaoUsuario);

            var provider =
                scope.ServiceProvider
                    .GetRequiredService<
                        IDataProtectionProvider>();

            var protetor =
                provider.CreateProtector(
                    "Kiora.Push.FcmToken.v1");

            // Confirma que o futuro serviço de envio poderá recuperar o token.
            Assert.Equal(
                TokenFicticio,
                protetor.Unprotect(
                    dispositivo.TokenFcm));
        }

        // Registrar novamente não cria duplicidade.
        using var repeticao =
            await Registrar(http);

        await ConfirmarSucesso(repeticao);

        var atualizado =
            await LerDispositivo(host);

        Assert.Equal(
            dispositivo.Id,
            atualizado.Id);

        Assert.Equal(
            dispositivo.TokenHash,
            atualizado.TokenHash);

        Assert.NotEqual(
            dispositivo.Versao,
            atualizado.Versao);

        Assert.Equal(
            1,
            await QuantidadeDispositivos(host));
    }

    // TESTE 3 — VALIDAÇÃO DOS TOKENS

    [Theory]
    [InlineData("")]
    [InlineData("token-curto")]
    [InlineData("TOKEN_FCM_INVALIDO COM ESPACO")]
    public async Task TokensInvalidosSaoRejeitados(
        string token)
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var resposta =
            await Registrar(http, token);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            resposta.StatusCode);

        Assert.Equal(
            0,
            await QuantidadeDispositivos(host));
    }

    // TESTE 4 — VISITANTE REALIZA LOGIN

    [Fact]
    public async Task VisitantePodeVincularDispositivoAConta()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente();

        await AbrirSessaoVisitante(http);

        using var registroVisitante =
            await Registrar(http);

        await ConfirmarSucesso(
            registroVisitante);

        var visitante =
            await LerDispositivo(host);

        Assert.Null(
            visitante.UsuarioId);

        Assert.NotNull(
            visitante.VisitanteHash);

        // Mantém a chave do visitante e acrescenta a autenticação JWT.
        Autenticar(http, host);

        using var registroCliente =
            await Registrar(http);

        await ConfirmarSucesso(
            registroCliente);

        var cliente =
            await LerDispositivo(host);

        Assert.Equal(
            visitante.Id,
            cliente.Id);

        Assert.Equal(
            visitante.VisitanteHash,
            cliente.VisitanteHash);

        Assert.NotNull(
            cliente.UsuarioId);

        Assert.NotNull(
            cliente.VersaoSessaoUsuario);

        Assert.NotEqual(
            visitante.Versao,
            cliente.Versao);

        Assert.True(cliente.Ativo);

        Assert.Equal(
            1,
            await QuantidadeDispositivos(host));
    }

    // TESTE 5 — PROTEÇÃO CONTRA TROCA INDEVIDA

    [Fact]
    public async Task OutraContaNaoPodeAssumirDispositivoAtivo()
    {
        await using var host =
            await HostMobile.Criar();

        using var titular =
            host.Cliente();

        await AbrirSessaoVisitante(titular);

        Autenticar(titular, host);

        using var registro =
            await Registrar(titular);

        await ConfirmarSucesso(registro);

        var original =
            await LerDispositivo(host);

        // Outra conta conhece o token FCM, mas não possui a chave original.
        using var outro =
            host.Cliente(cookies: false);

        Autenticar(
            outro,
            host,
            outroCliente: true);

        using var tentativa =
            await Registrar(outro);

        Assert.Equal(
            HttpStatusCode.Conflict,
            tentativa.StatusCode);

        var json =
            await tentativa.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.False(
            json.GetProperty("sucesso")
                .GetBoolean());

        Assert.True(
            json.GetProperty("renovarToken")
                .GetBoolean());

        // Mesmo com a chave original, outra conta não pode substituir uma associação ativa e válida.
        Autenticar(
            titular,
            host,
            outroCliente: true);

        using var troca =
            await Registrar(titular);

        Assert.Equal(
            HttpStatusCode.Conflict,
            troca.StatusCode);

        var atual =
            await LerDispositivo(host);

        Assert.Equal(
            original.UsuarioId,
            atual.UsuarioId);

        Assert.Equal(
            original.VersaoSessaoUsuario,
            atual.VersaoSessaoUsuario);

        Assert.Equal(
            original.Versao,
            atual.Versao);

        Assert.True(atual.Ativo);

        Assert.Equal(
            1,
            await QuantidadeDispositivos(host));
    }

    // TESTE 6 — DESATIVAÇÃO E TROCA DE CONTA

    [Fact]
    public async Task SomenteTitularPodeDesativarDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        using var titular =
            host.Cliente();

        await AbrirSessaoVisitante(titular);

        Autenticar(titular, host);

        using var registro =
            await Registrar(titular);

        await ConfirmarSucesso(registro);

        var original =
            await LerDispositivo(host);

        // Outra conta não pode desativar.
        using var outro =
            host.Cliente(cookies: false);

        Autenticar(
            outro,
            host,
            outroCliente: true);

        using var tentativa =
            await Desativar(outro);

        // Resposta idempotente que não revela se o dispositivo pertence a terceiros.
        await ConfirmarSucesso(tentativa);

        Assert.True(
            (await LerDispositivo(host)).Ativo);

        // Remove o JWT, mantendo a identificação original do visitante.
        titular.DefaultRequestHeaders.Authorization =
            null;

        await AbrirSessaoVisitante(titular);

        using var visitante =
            await Desativar(titular);

        await ConfirmarSucesso(visitante);

        // Visitante não pode desativar dispositivo de uma conta autenticada.
        var preservado =
            await LerDispositivo(host);

        Assert.True(preservado.Ativo);

        Assert.Equal(
            original.Versao,
            preservado.Versao);

        // O titular autenticado pode desativar.
        Autenticar(titular, host);

        using var desativacao =
            await Desativar(titular);

        await ConfirmarSucesso(desativacao);

        var inativo =
            await LerDispositivo(host);

        Assert.False(inativo.Ativo);

        Assert.NotNull(
            inativo.DesativadoEmUtc);

        Assert.NotEqual(
            original.Versao,
            inativo.Versao);

        // Repetir não modifica novamente.
        using var repeticao =
            await Desativar(titular);

        await ConfirmarSucesso(repeticao);

        Assert.Equal(
            inativo.Versao,
            (await LerDispositivo(host)).Versao);

        // Após desativar, permite vincular outra conta com a chave do visitante.
        Autenticar(
            titular,
            host,
            outroCliente: true);

        using var novaAssociacao =
            await Registrar(titular);

        await ConfirmarSucesso(novaAssociacao);

        var novo =
            await LerDispositivo(host);

        Assert.Equal(
            original.Id,
            novo.Id);

        Assert.NotEqual(
            original.UsuarioId,
            novo.UsuarioId);

        Assert.True(novo.Ativo);

        Assert.Null(
            novo.DesativadoEmUtc);
    }

    // TESTE 7 — REVOGAÇÃO DA SESSÃO

    [Fact]
    public async Task SessaoRevogadaNaoPodeRegistrarDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente();

        await AbrirSessaoVisitante(http);

        Autenticar(http, host);

        using var registro =
            await Registrar(http);

        await ConfirmarSucesso(registro);

        var original =
            await LerDispositivo(host);

        var novaVersao =
            Guid.NewGuid();

        // Simula a revogação da sessão anteriormente autenticada.
        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var usuario =
                await db.Usuarios.SingleAsync(
                    u => u.Id ==
                        original.UsuarioId!.Value);

            usuario.VersaoSessao =
                novaVersao;

            await db.SaveChangesAsync();
        }

        // JWT anterior não pode registrar.
        using var registroAntigo =
            await Registrar(http);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            registroAntigo.StatusCode);

        // JWT anterior não pode desativar.
        using var desativacaoAntiga =
            await Desativar(http);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            desativacaoAntiga.StatusCode);

        var preservado =
            await LerDispositivo(host);

        Assert.Equal(
            original.Versao,
            preservado.Versao);

        Assert.NotEqual<Guid?>(
            novaVersao,
            preservado.VersaoSessaoUsuario);

        // Outra conta com a chave original pode assumir a associação revogada.
        Autenticar(
            http,
            host,
            outroCliente: true);

        using var novaAssociacao =
            await Registrar(http);

        await ConfirmarSucesso(novaAssociacao);

        var atualizado =
            await LerDispositivo(host);

        Assert.NotEqual(
            original.UsuarioId,
            atualizado.UsuarioId);

        Assert.True(atualizado.Ativo);

        Assert.NotEqual(
            original.Versao,
            atualizado.Versao);

        Assert.Equal(
            1,
            await QuantidadeDispositivos(host));
    }

    // TESTE 8 — CONTROLE DE CONCORRÊNCIA

    [Fact]
    public async Task AlteracaoAntigaNaoSobrescreveDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        using var http =
            host.Cliente(cookies: false);

        Autenticar(http, host);

        using var registro =
            await Registrar(http);

        await ConfirmarSucesso(registro);

        // Mantém uma versão antiga do dispositivo carregada em um contexto independente.
        await using var scopeAntigo =
            host.App.Services.CreateAsyncScope();

        var dbAntigo =
            scopeAntigo.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var dispositivoAntigo =
            await dbAntigo.DispositivosPush
                .SingleAsync();

        var versaoAntiga =
            dispositivoAntigo.Versao;

        // Outra operação atualiza o dispositivo.
        using var novoRegistro =
            await Registrar(http);

        await ConfirmarSucesso(novoRegistro);

        // Tenta gravar uma alteração utilizando a versão anteriormente carregada.
        dispositivoAntigo.Ativo = false;

        dispositivoAntigo.Versao =
            Guid.NewGuid();

        await Assert.ThrowsAsync<
            DbUpdateConcurrencyException>(
                async () =>
                {
                    await dbAntigo.SaveChangesAsync();
                });

        var atual =
            await LerDispositivo(host);

        Assert.True(atual.Ativo);

        Assert.NotEqual(
            versaoAntiga,
            atual.Versao);
    }
}