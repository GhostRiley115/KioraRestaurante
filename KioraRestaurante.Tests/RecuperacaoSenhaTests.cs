using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using KioraRestaurante.Controllers;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Filters;
using KioraRestaurante.Models;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KioraRestaurante.Tests;

internal sealed class EmailServiceFalso(bool sucesso = true) : IEmailService
{
    public List<(string Email, string Codigo)> Envios { get; } = [];
    public Task<bool> EnviarCodigoRecuperacaoSenhaAsync(string emailDestino, string codigo)
    {
        Envios.Add((emailDestino, codigo));
        return Task.FromResult(sucesso);
    }
}

public class RecuperacaoSenhaTests
{
    private const string SenhaOriginal = "Original123!";
    private const string NovaSenha = "NovaSenha456!";
    private static UsuarioRedefinirSenhaDTO Redefinicao(string email, string codigo) => new()
        { Email = email, Token = codigo, NovaSenha = NovaSenha, ConfirmarNovaSenha = NovaSenha };

    [Fact]
    public void CodigoEProtegidoExpiraEIdentificaSomenteAContaInformada()
    {
        using var banco = new Banco();
        var inicio = DateTime.UtcNow;
        var gerado = banco.Servico.GerarTokenRecuperacao(new() { Email = banco.Primeiro.Email });
        Assert.NotNull(gerado); Assert.Matches("^[0-9]{6}$", gerado.Codigo);
        var salvo = banco.Ler(banco.Primeiro.Id);
        Assert.StartsWith("otp:", salvo.TokenRecuperacaoSenha);
        Assert.NotEqual(gerado.Codigo, salvo.TokenRecuperacaoSenha);
        Assert.InRange(salvo.ExpiracaoTokenRecuperacaoSenha!.Value, inicio.AddMinutes(9), inicio.AddMinutes(11));
        Assert.True(banco.Servico.ValidarCodigoRecuperacao(gerado.Email, gerado.Codigo));
        Assert.False(banco.Servico.ValidarCodigoRecuperacao(banco.Segundo.Email, gerado.Codigo));
        Assert.False(banco.Servico.RedefinirSenha(Redefinicao(banco.Segundo.Email, gerado.Codigo)));
        Assert.False(banco.Servico.RedefinirSenha(Redefinicao("", gerado.Codigo)));
        Assert.True(banco.Servico.RedefinirSenha(Redefinicao(gerado.Email, gerado.Codigo)));
        Assert.False(banco.Servico.RedefinirSenha(Redefinicao(gerado.Email, gerado.Codigo)));
        Assert.NotEqual(banco.Primeiro.VersaoSessao, banco.Ler(banco.Primeiro.Id).VersaoSessao);
    }

    [Fact]
    public void CodigoIgualEmDuasContasNaoRedefineAOutraConta()
    {
        using var banco = new Banco();
        var hasher = new PasswordHasher<Usuario>();
        foreach (var u in new[] { banco.Primeiro, banco.Segundo })
        {
            var hash = "otp:" + hasher.HashPassword(u, "123456");
            banco.Contexto.Usuarios.Where(x => x.Id == u.Id).ExecuteUpdate(set =>
                set.SetProperty(x => x.TokenRecuperacaoSenha, hash)
                    .SetProperty(x => x.ExpiracaoTokenRecuperacaoSenha, DateTime.UtcNow.AddMinutes(10)));
        }
        Assert.True(banco.Servico.RedefinirSenha(Redefinicao(banco.Segundo.Email, "123456")));
        Assert.NotNull(banco.Servico.Autenticar(new() { Email = banco.Primeiro.Email, Senha = SenhaOriginal }));
        Assert.NotNull(banco.Servico.Autenticar(new() { Email = banco.Segundo.Email, Senha = NovaSenha }));
    }

    [Theory]
    [InlineData("expirado")]
    [InlineData("inativo")]
    [InlineData("legado")]
    [InlineData("malformado")]
    public void CodigoInvalidoNaoTrocaSenha(string cenario)
    {
        using var banco = new Banco();
        var gerado = banco.Servico.GerarTokenRecuperacao(new() { Email = banco.Primeiro.Email })!;
        var query = banco.Contexto.Usuarios.Where(u => u.Id == banco.Primeiro.Id);
        if (cenario == "expirado") query.ExecuteUpdate(set => set.SetProperty(u => u.ExpiracaoTokenRecuperacaoSenha, DateTime.UtcNow.AddSeconds(-1)));
        if (cenario == "inativo") query.ExecuteUpdate(set => set.SetProperty(u => u.Ativo, false));
        if (cenario == "legado") query.ExecuteUpdate(set => set.SetProperty(u => u.TokenRecuperacaoSenha, gerado.Codigo));
        if (cenario == "malformado") query.ExecuteUpdate(set => set.SetProperty(u => u.TokenRecuperacaoSenha, "otp:invalido"));
        Assert.False(banco.Servico.ValidarCodigoRecuperacao(gerado.Email, gerado.Codigo));
        Assert.False(banco.Servico.RedefinirSenha(Redefinicao(gerado.Email, gerado.Codigo)));
        Assert.Equal(banco.Primeiro.SenhaHash, banco.Ler(banco.Primeiro.Id).SenhaHash);
    }

    [Fact]
    public void CancelamentoDeEnvioAntigoNaoApagaNovoCodigo()
    {
        using var banco = new Banco();
        var anterior = banco.Servico.GerarTokenRecuperacao(new() { Email = banco.Primeiro.Email })!;
        var novo = banco.Servico.GerarTokenRecuperacao(new() { Email = banco.Primeiro.Email })!;
        Assert.NotEqual(anterior.Identificador, novo.Identificador);
        banco.Servico.InvalidarCodigoRecuperacao(anterior);
        Assert.Equal(novo.Identificador, banco.Ler(banco.Primeiro.Id).TokenRecuperacaoSenha);
        Assert.True(banco.Servico.ValidarCodigoRecuperacao(novo.Email, novo.Codigo));
        banco.Servico.InvalidarCodigoRecuperacao(novo);
        Assert.False(banco.Servico.ValidarCodigoRecuperacao(novo.Email, novo.Codigo));
    }

    [Fact]
    public async Task EnvioUsaEmailCadastradoERespostaNaoRevelaCodigoOuExistencia()
    {
        using var banco = new Banco();
        var email = new EmailServiceFalso();
        var controller = new ContaController(banco.Servico, email);
        var existente = Assert.IsType<OkObjectResult>(await controller.EsqueciSenha(new() { Email = "  PRIMEIRO@EXAMPLE.TEST  " }));
        var ausente = Assert.IsType<OkObjectResult>(await controller.EsqueciSenha(new() { Email = "ausente@example.test" }));
        Assert.Equal(JsonSerializer.Serialize(existente.Value), JsonSerializer.Serialize(ausente.Value));
        var envio = Assert.Single(email.Envios);
        Assert.Equal(banco.Primeiro.Email, envio.Email);
        Assert.DoesNotContain(envio.Codigo, JsonSerializer.Serialize(existente.Value));
    }

    [Fact]
    public async Task FalhaDeEnvioCancelaCodigoSemErro500OuEnumeracao()
    {
        using var banco = new Banco();
        var email = new EmailServiceFalso(false);
        var controller = new ContaController(banco.Servico, email);
        var conhecido = Assert.IsType<OkObjectResult>(await controller.EsqueciSenha(new() { Email = banco.Primeiro.Email }));
        var ausente = Assert.IsType<OkObjectResult>(await controller.EsqueciSenha(new() { Email = "ausente@example.test" }));
        Assert.Equal(JsonSerializer.Serialize(conhecido.Value), JsonSerializer.Serialize(ausente.Value));
        Assert.Null(banco.Ler(banco.Primeiro.Id).TokenRecuperacaoSenha);
    }

    [Fact]
    public async Task ConferenciasCompartilhamLimitePorContaEEnviosTemLimiteSeparado()
    {
        using var banco = new Banco();
        using var limitador = new LimitadorRecuperacaoSenha();
        var filtro = new LimitarRecuperacaoSenha(limitador, banco.Contexto);
        for (var i = 0; i < 10; i++)
        {
            object model = i % 2 == 0
                ? new ValidarCodigoRecuperacaoViewModel { Email = banco.Primeiro.Email, Codigo = "000000" }
                : new RedefinirSenhaViewModel { Email = "PRIMEIRO@EXAMPLE.TEST", Codigo = "000000" };
            Assert.Null((await Aplicar(filtro, model)).Result);
        }
        var bloqueado = await Aplicar(filtro, new RedefinirSenhaViewModel { Email = banco.Primeiro.Email });
        Assert.Equal(429, Assert.IsType<JsonResult>(bloqueado.Result).StatusCode);
        Assert.True(int.Parse(bloqueado.HttpContext.Response.Headers.RetryAfter!) > 0);
        for (var i = 0; i < 3; i++) Assert.Null((await Aplicar(filtro, new EsqueciSenhaViewModel { Email = banco.Primeiro.Email })).Result);
        Assert.Equal(429, Assert.IsType<JsonResult>((await Aplicar(filtro, new EsqueciSenhaViewModel { Email = banco.Primeiro.Email })).Result).StatusCode);
        Assert.Null((await Aplicar(filtro, new ValidarCodigoRecuperacaoViewModel { Email = banco.Segundo.Email })).Result);
    }

    [Theory]
    [InlineData(201, true)]
    [InlineData(401, false)]
    [InlineData(500, false)]
    public async Task BrevoUsaContratoEsperadoSemEnviarEmailReal(int status, bool esperado)
    {
        var handler = new TransporteFalso(status);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Brevo:ApiKey"] = "chave-ficticia", ["Brevo:RemetenteEmail"] = "envio@example.test", ["Brevo:RemetenteNome"] = "Kiora"
        }).Build();
        var servico = new EmailService(new HttpClient(handler), config, NullLogger<EmailService>.Instance);
        Assert.Equal(esperado, await servico.EnviarCodigoRecuperacaoSenhaAsync("cliente@example.test", "123456"));
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Url);
        using var json = JsonDocument.Parse(handler.Corpo!);
        Assert.Equal("cliente@example.test", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Contains("123456", json.RootElement.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task ConfiguracaoAusenteNaoFazChamadaExterna()
    {
        var handler = new TransporteFalso(201);
        var servico = new EmailService(new HttpClient(handler), new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance);
        Assert.False(await servico.EnviarCodigoRecuperacaoSenhaAsync("cliente@example.test", "123456"));
        Assert.Null(handler.Url);
    }

    private static async Task<ActionExecutingContext> Aplicar(LimitarRecuperacaoSenha filtro, object model)
    {
        var http = new DefaultHttpContext();
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?> { ["model"] = model }, new object());
        await filtro.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(action, [], new object())));
        return context;
    }

    private sealed class TransporteFalso(int status) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Corpo { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString(); Corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage((HttpStatusCode)status);
        }
    }

    private sealed class Banco : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        public AppDbContext Contexto { get; }
        public UsuarioService Servico { get; }
        public Usuario Primeiro { get; }
        public Usuario Segundo { get; }
        public Banco()
        {
            _conexao.Open();
            Contexto = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
            Contexto.Database.EnsureCreated();
            var hasher = new PasswordHasher<Usuario>();
            Primeiro = new Usuario { Nome = "Primeiro", Email = "primeiro@example.test" };
            Segundo = new Usuario { Nome = "Segundo", Email = "segundo@example.test" };
            foreach (var usuario in new[] { Primeiro, Segundo }) { usuario.SenhaHash = hasher.HashPassword(usuario, SenhaOriginal); Contexto.Add(usuario); }
            Contexto.SaveChanges(); Servico = new UsuarioService(Contexto);
        }
        public Usuario Ler(int id) => Contexto.Usuarios.AsNoTracking().Single(u => u.Id == id);
        public void Dispose() { Contexto.Dispose(); _conexao.Dispose(); }
    }
}
