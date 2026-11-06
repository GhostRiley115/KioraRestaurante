using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Models;
using KioraRestaurante.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services.Entrega;

public class HeiGitRotasEntrega(HttpClient http, IOptions<EntregaOptions> options,
    IMemoryCache cache, ILogger<HeiGitRotasEntrega> logger, IGeocodificadorEntrega geocodificador) : IRotasEntrega
{
    private static string Chave(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private async Task<JsonDocument> Consultar(HttpRequestMessage req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Rotas.ApiKey))
            throw new ValidationException("O cálculo de entrega está indisponível. Entre em contato com o restaurante.");

        req.Headers.TryAddWithoutValidation("Authorization", options.Value.Rotas.ApiKey);
        try
        {
            using var resposta = await http.SendAsync(req, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                // Registra somente o código: não grava endereço nem chave de acesso nos logs.
                logger.LogWarning("Serviço de rotas respondeu HTTP {StatusCode}.", (int)resposta.StatusCode);
                throw new ValidationException("Não foi possível calcular a entrega agora. Tente novamente em instantes.");
            }
            return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ValidationException("Não foi possível consultar a rota. Tente novamente em instantes.");
        }
    }

    public async Task<decimal> CalcularMetros(EnderecoEntrega destino, CancellationToken ct = default)
    {
        var restaurante = options.Value.Restaurante;
        var origem = restaurante.EnderecoCompleto;
        var endereco = $"{destino.Logradouro}, {destino.Numero}, {destino.Bairro}, {destino.Cidade}, {destino.Uf}, {destino.Cep}, Brasil";
        var chave = "entrega:rota:" + Chave(origem + "|" + endereco);
        if (cache.TryGetValue<decimal>(chave, out var salvo)) return salvo;

        var partida = await geocodificador.Localizar(new(restaurante.Logradouro, restaurante.Numero, restaurante.Bairro, restaurante.Cidade, restaurante.Uf), ct);
        var chegada = await geocodificador.Localizar(new(destino.Logradouro, destino.Numero, destino.Bairro, destino.Cidade, destino.Uf, destino.Cep), ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, "openrouteservice/v2/directions/driving-car/json")
        {
            Content = JsonContent.Create(new
            {
                coordinates = new[] { new[] { partida.Longitude, partida.Latitude }, new[] { chegada.Longitude, chegada.Latitude } },
                preference = "shortest", units = "m", instructions = false, geometry = false,
                radiuses = new[] { 100, 100 }
            })
        };
        using var json = await Consultar(req, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("routes", out var rotas) || rotas.ValueKind != JsonValueKind.Array
            || rotas.GetArrayLength() == 0 || rotas[0].ValueKind != JsonValueKind.Object
            || !rotas[0].TryGetProperty("summary", out var resumo) || resumo.ValueKind != JsonValueKind.Object
            || !resumo.TryGetProperty("distance", out var distancia) || distancia.ValueKind != JsonValueKind.Number
            || !distancia.TryGetDecimal(out var metros) || metros < 0)
            throw new ValidationException("Não foi encontrada uma rota de entrega para esse endereço.");

        cache.Set(chave, metros, TimeSpan.FromMinutes(15));
        return metros;
    }
}
