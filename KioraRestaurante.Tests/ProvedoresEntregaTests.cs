using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Models;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Entrega;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Opcoes = Microsoft.Extensions.Options.Options;

namespace KioraRestaurante.Tests;

public class ProvedoresEntregaTests
{
    private static readonly EnderecoRota Endereco = new("Rua dos Pinheiros", "1234", "Pinheiros", "São Paulo", "SP", "05422-012");

    private static string Resultado(string tipo = "building", string rua = "Rua dos Pinheiros", string numero = "1234", double precisao = .75) => JsonSerializer.Serialize(new
    {
        results = new[] { new { result_type = tipo, country_code = "br", state_code = "SP", city = "São Paulo",
            suburb = "Pinheiros", street = rua, housenumber = numero, postcode = "05422-012",
            rank = new { confidence_building_level = precisao }, lon = -46.6893574, lat = -23.5681155 } }
    });

    [Fact]
    public async Task LocalizacaoPrecisaEReutilizadaSemNovaConsulta()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new Resposta(Resultado());
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.geoapify.com/") };
        var geo = CriarGeo(http, cache);
        var ponto = await geo.Localizar(Endereco);
        Assert.Equal(-46.6893574, ponto.Longitude);
        Assert.Equal(ponto, await geo.Localizar(Endereco));
        Assert.Equal(1, handler.Consultas);
    }

    [Theory]
    [InlineData("street", "Rua dos Pinheiros", "1234", 1)]
    [InlineData("postcode", "Rua dos Pinheiros", "1234", 1)]
    [InlineData("building", "Rua dos Pinheiros", "999", 1)]
    [InlineData("building", "Rua Lázaro Augusto Alves", "1234", 1)]
    [InlineData("building", "Rua dos Pinheiros", "1234", .69)]
    public async Task NaoConfundeBairroCepRuaOuOutroNumeroComEnderecoPreciso(string tipo, string rua, string numero, double precisao)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new Resposta(Resultado(tipo, rua, numero, precisao));
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.geoapify.com/") };
        await Assert.ThrowsAsync<ValidationException>(() => CriarGeo(http, cache).Localizar(Endereco));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"results\":[null,1,{}]}")]
    [InlineData("JSON inválido")]
    public async Task GeocodificadorComRespostaInvalidaProduzMensagemControlada(string corpo)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new Resposta(corpo);
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.geoapify.com/") };
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CriarGeo(http, cache).Localizar(Endereco));
        Assert.DoesNotContain("chave-ficticia", ex.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"routes\":[null]}")]
    [InlineData("{\"routes\":[{\"summary\":null}]}")]
    [InlineData("{\"routes\":[{\"summary\":{\"distance\":\"\"}}]}")]
    [InlineData("{\"routes\":[{\"summary\":{\"distance\":-1}}]}")]
    public async Task RotaInvalidaNaoGeraErro500NemEntregaGratuita(string corpo)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new Resposta(corpo);
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.heigit.org/") };
        var rota = new HeiGitRotasEntrega(http, Config(), cache, NullLogger<HeiGitRotasEntrega>.Instance, new GeoSimulado());
        await Assert.ThrowsAsync<ValidationException>(() => rota.CalcularMetros(new EnderecoEntrega()));
    }

    [Fact]
    public async Task RotaUsaPercursoDeCarroEmMetrosEChaveNoCabecalho()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new Resposta("{\"routes\":[{\"summary\":{\"distance\":4474.7}}]}");
        using var http = new HttpClient(handler) { BaseAddress = new("https://api.heigit.org/") };
        var rota = new HeiGitRotasEntrega(http, Config(), cache, NullLogger<HeiGitRotasEntrega>.Instance, new GeoSimulado());
        Assert.Equal(4474.7m, await rota.CalcularMetros(new EnderecoEntrega()));
        Assert.Equal("chave-ficticia", handler.Autorizacao);
        Assert.Equal("https://api.heigit.org/openrouteservice/v2/directions/driving-car/json", handler.Url);
        using var corpo = JsonDocument.Parse(handler.Corpo!);
        Assert.Equal("shortest", corpo.RootElement.GetProperty("preference").GetString());
        Assert.Equal("m", corpo.RootElement.GetProperty("units").GetString());
        Assert.Equal(2, corpo.RootElement.GetProperty("coordinates").GetArrayLength());
    }

    private static Microsoft.Extensions.Options.IOptions<EntregaOptions> Config() => Opcoes.Create(new EntregaOptions
    { Rotas = new() { ApiKey = "chave-ficticia" }, Geocodificacao = new() { ApiKey = "chave-ficticia" } });
    private static GeoapifyGeocodificadorEntrega CriarGeo(HttpClient http, IMemoryCache cache) => new(http, Config(), cache, NullLogger<GeoapifyGeocodificadorEntrega>.Instance);

    private sealed class GeoSimulado : IGeocodificadorEntrega
    {
        public Task<PontoEntrega> Localizar(EnderecoRota endereco, CancellationToken ct = default) => Task.FromResult(new PontoEntrega(-46.68, -23.56));
    }

    private sealed class Resposta(string corpo) : HttpMessageHandler
    {
        public int Consultas { get; private set; }
        public string? Autorizacao { get; private set; }
        public string? Url { get; private set; }
        public string? Corpo { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Consultas++;
            Url = request.RequestUri!.ToString();
            Autorizacao = request.Headers.TryGetValues("Authorization", out var valores) ? valores.Single() : null;
            Corpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }
}
