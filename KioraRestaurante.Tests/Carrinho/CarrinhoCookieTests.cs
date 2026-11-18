using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Xunit;
using KioraRestaurante.Services.Carrinhos;

namespace KioraRestaurante.Tests;

public class CarrinhoCookieTests
{
    [Theory]
    [InlineData("Development", "http", "KioraCarrinhoDesenvolvimento", false)]
    [InlineData("Development", "https", "__Host-KioraCarrinho", true)]
    [InlineData("Production", "https", "__Host-KioraCarrinho", true)]
    [InlineData("Production", "http", "__Host-KioraCarrinho", true)]
    public void MantemCarrinhoEntreRequisicoesSemRemoverProtecaoEmProducao(string ambiente, string esquema, string nome, bool seguro)
    {
        var service = new CarrinhoCookie(new EphemeralDataProtectionProvider(), new Ambiente { EnvironmentName = ambiente });
        var resposta = new DefaultHttpContext();
        resposta.Request.Scheme = esquema;
        service.Gravar(resposta, 42);
        var cookie = resposta.Response.Headers.SetCookie.ToString();
        Assert.StartsWith(nome + "=", cookie);
        Assert.Equal(seguro, cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=lax", cookie);
        Assert.Contains("path=/", cookie);
        var seguinte = new DefaultHttpContext();
        seguinte.Request.Scheme = esquema;
        seguinte.Request.Headers.Cookie = cookie.Split(';')[0];
        Assert.Equal(42, service.Ler(seguinte));
        service.Remover(seguinte);
        Assert.StartsWith(nome + "=;", seguinte.Response.Headers.SetCookie.ToString());
        Assert.Equal(seguro, seguinte.Response.Headers.SetCookie.ToString().Contains("; secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProducaoNaoAceitaCookieLocalNemValorAdulterado()
    {
        var provider = new EphemeralDataProtectionProvider();
        var local = new CarrinhoCookie(provider, new Ambiente { EnvironmentName = "Development" });
        var producao = new CarrinhoCookie(provider, new Ambiente { EnvironmentName = "Production" });
        var http = new DefaultHttpContext();
        local.Gravar(http, 42);
        http.Request.Headers.Cookie = http.Response.Headers.SetCookie.ToString().Split(';')[0];
        Assert.Null(producao.Ler(http));
        http.Request.Headers.Cookie = "__Host-KioraCarrinho=42";
        Assert.Null(producao.Ler(http));
    }

    private sealed class Ambiente : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Teste";
        public string ContentRootPath { get; set; } = ".";
        public string WebRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
