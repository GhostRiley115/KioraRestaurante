using System.Net;
using System.Globalization;
using System.Text.Json;
using KioraRestaurante.Filters;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace KioraRestaurante.Tests;

public class LoginRateLimitTests
{
    [Fact]
    public void SextaTentativaDoMesmoEmailEhBloqueadaMesmoMudandoIp()
    {
        using var limitador = new LimitadorLogin();

        for (var i = 1; i <= 5; i++)
            Assert.True(Tentar(limitador, $"192.0.2.{i}", "cliente@teste.com"));

        Assert.False(Tentar(limitador, "192.0.2.6", "cliente@teste.com"));
    }

    [Fact]
    public void EmailComMaiusculasEEspacosUsaOMesmoLimite()
    {
        using var limitador = new LimitadorLogin();

        for (var i = 0; i < 5; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", "cliente@teste.com"));

        Assert.False(Tentar(limitador, "192.0.2.2", "  CLIENTE@TESTE.COM  "));
    }

    [Fact]
    public void VigesimaPrimeiraTentativaDoIpEhBloqueadaMesmoMudandoEmail()
    {
        using var limitador = new LimitadorLogin(limitarPorIp: true);

        for (var i = 0; i < 20; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", $"cliente{i}@teste.com"));

        Assert.False(Tentar(limitador, "192.0.2.1", "outro@teste.com"));
        Assert.True(Tentar(limitador, "192.0.2.2", "outro@teste.com"));
    }

    [Fact]
    public void BloquearUmEmailNaoBloqueiaOutroNaMesmaRede()
    {
        using var limitador = new LimitadorLogin();

        for (var i = 0; i < 5; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", "cliente@teste.com"));

        Assert.False(Tentar(limitador, "192.0.2.1", "cliente@teste.com"));
        Assert.True(Tentar(limitador, "192.0.2.1", "outro@teste.com"));
    }

    [Fact]
    public void IpV4EIpV6MapeadoCompartilhamOLimite()
    {
        using var limitador = new LimitadorLogin(limitarPorIp: true);

        for (var i = 0; i < 20; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", $"cliente{i}@teste.com"));

        Assert.False(Tentar(limitador, "::ffff:192.0.2.1", "novo@teste.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmailAusenteNaoCriaBloqueioCompartilhadoEntreIps(string? email)
    {
        using var limitador = new LimitadorLogin(limitarPorIp: true);

        for (var i = 0; i < 20; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", email));

        Assert.False(Tentar(limitador, "192.0.2.1", email));
        Assert.True(Tentar(limitador, "192.0.2.2", email));
    }

    [Fact]
    public async Task LimiteDoEmailEhMantidoComTentativasSimultaneas()
    {
        using var limitador = new LimitadorLogin();
        var resultados = await Task.WhenAll(Enumerable.Range(1, 50).Select(i =>
            Task.Run(() => Tentar(limitador, $"192.0.2.{i}", "cliente@teste.com"))));

        Assert.Equal(5, resultados.Count(permitido => permitido));
    }

    [Fact]
    public async Task LimiteDoIpEhMantidoComTentativasSimultaneas()
    {
        using var limitador = new LimitadorLogin(limitarPorIp: true);
        var resultados = await Task.WhenAll(Enumerable.Range(1, 50).Select(i =>
            Task.Run(() => Tentar(limitador, "192.0.2.1", $"cliente{i}@teste.com"))));

        Assert.Equal(20, resultados.Count(permitido => permitido));
    }

    [Fact]
    public async Task FiltroRetorna429ComJsonETempoDeEsperaSemExecutarLogin()
    {
        using var banco = CriarBanco();
        using var limitador = new LimitadorLogin();
        for (var i = 0; i < 5; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", "cliente@teste.com"));

        var context = CriarContexto("cliente@teste.com");
        await new LimitarTentativasLogin(limitador, banco).OnActionExecutionAsync(context,
            () => throw new InvalidOperationException("Não deve consultar a senha após atingir o limite."));

        var resposta = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, resposta.StatusCode);
        var json = JsonSerializer.SerializeToElement(resposta.Value);
        Assert.False(json.GetProperty("sucesso").GetBoolean());
        Assert.Contains("segundos", json.GetProperty("mensagem").GetString());
        var espera = int.Parse(context.HttpContext.Response.Headers.RetryAfter.ToString());
        Assert.InRange(espera, 1, 60);
    }

    [Fact]
    public async Task FiltroPermiteExecutarLoginAntesDeAtingirOLimite()
    {
        using var banco = CriarBanco();
        using var limitador = new LimitadorLogin();
        var context = CriarContexto("cliente@teste.com");
        var executou = false;

        await new LimitarTentativasLogin(limitador, banco).OnActionExecutionAsync(context, () =>
        {
            executou = true;
            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
        });

        Assert.True(executou);
        Assert.Null(context.Result);
        Assert.False(context.HttpContext.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task EmailMuitoGrandeEhRecusadoAntesDeExecutarLoginEContaNoLimiteDoIp()
    {
        using var banco = CriarBanco();
        using var limitador = new LimitadorLogin(limitarPorIp: true);
        var filtro = new LimitarTentativasLogin(limitador, banco);

        for (var i = 0; i < 20; i++)
        {
            var context = CriarContexto(new string('a', 151) + "@teste.com");
            await filtro.OnActionExecutionAsync(context,
                () => throw new InvalidOperationException("E-mail grande demais não deve chegar ao login."));
            Assert.IsType<BadRequestObjectResult>(context.Result);
        }

        Assert.False(Tentar(limitador, "192.0.2.1", "cliente@teste.com"));
    }

    [Fact]
    public async Task AlterarXForwardedForNaoContornaOLimiteDaConexao()
    {
        using var banco = CriarBanco();
        using var limitador = new LimitadorLogin(limitarPorIp: true);
        var filtro = new LimitarTentativasLogin(limitador, banco);
        var executados = 0;

        for (var i = 1; i <= 21; i++)
        {
            var context = CriarContexto($"cliente{i}@teste.com");
            context.HttpContext.Request.Headers["X-Forwarded-For"] = $"198.51.100.{i}";
            await filtro.OnActionExecutionAsync(context, () =>
            {
                executados++;
                return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller));
            });

            if (i == 21)
                Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<JsonResult>(context.Result).StatusCode);
        }

        Assert.Equal(20, executados);
    }

    [Fact]
    public void PadraoNaoBloqueiaClientesDiferentesQueCompartilhamOIpDoProxy()
    {
        using var limitador = new LimitadorLogin();

        for (var i = 0; i < 50; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", $"cliente{i}@teste.com"));
    }

    [Fact]
    public async Task VariantesQueOBancoConsideraEquivalentesCompartilhamOLimite()
    {
        using var banco = CriarBanco();
        banco.Usuarios.Add(new Usuario { Nome = "Cliente", Email = "clíente@teste.com", SenhaHash = "nao-utilizado" });
        banco.SaveChanges();
        using var limitador = new LimitadorLogin();
        for (var i = 0; i < 5; i++)
            Assert.True(Tentar(limitador, "192.0.2.1", "clíente@teste.com"));
        var contexto = CriarContexto("cliente@teste.com");
        await new LimitarTentativasLogin(limitador, banco).OnActionExecutionAsync(contexto,
            () => throw new InvalidOperationException("A variação do e-mail não pode liberar outra tentativa."));
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<JsonResult>(contexto.Result).StatusCode);
    }

    // Simula a comparação sem diferença de acentos confirmada no MySQL local.
    private sealed class BancoLogin(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Usuario>().Property(u => u.Email).UseCollation("NOCASE");
        }
    }

    private static AppDbContext CriarBanco()
    {
        var banco = new BancoLogin(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        banco.Database.OpenConnection();
        ((SqliteConnection)banco.Database.GetDbConnection()).CreateCollation("NOCASE",
            (primeiro, segundo) => CultureInfo.InvariantCulture.CompareInfo.Compare(primeiro, segundo,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        banco.Database.EnsureCreated();
        return banco;
    }

    private static bool Tentar(LimitadorLogin limitador, string ip, string? email)
    {
        using var permissao = limitador.AdquirirPermissao(IPAddress.Parse(ip), email);
        return permissao.IsAcquired;
    }

    private static ActionExecutingContext CriarContexto(string email)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        return new ActionExecutingContext(action, new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["model"] = new LoginViewModel { Email = email, Senha = "senha" } },
            new object());
    }
}
