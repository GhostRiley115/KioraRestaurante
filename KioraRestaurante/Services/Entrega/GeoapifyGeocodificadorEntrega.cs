using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KioraRestaurante.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services.Entrega;

public class GeoapifyGeocodificadorEntrega(HttpClient http, IOptions<EntregaOptions> options,
    IMemoryCache cache, ILogger<GeoapifyGeocodificadorEntrega> logger) : IGeocodificadorEntrega
{
    private static string Texto(JsonElement e, string nome) => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
    private static string Normalizar(string s) => Regex.Replace(
        s.Normalize(NormalizationForm.FormD), @"\p{Mn}|[^\p{L}\p{N}]", "").ToUpperInvariant();
    private static string Rua(string s) => Normalizar(Regex.Replace(s.Trim(),
        @"^(Rua|R\.?|Avenida|Av\.?|Travessa|Tv\.?|Alameda|Al\.?)\s+", "", RegexOptions.IgnoreCase));
    private static bool Numero(JsonElement e, string nome, out double numero)
    {
        numero = 0;
        return e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out numero);
    }

    public async Task<PontoEntrega> Localizar(EnderecoRota endereco, CancellationToken ct = default)
    {
        var chave = "entrega:geoapify:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endereco))));
        if (cache.TryGetValue<PontoEntrega>(chave, out var salvo) && salvo != null) return salvo;
        var apiKey = options.Value.Geocodificacao.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ValidationException("O cálculo de entrega está indisponível. Entre em contato com o restaurante.");

        var texto = $"{endereco.Logradouro}, {endereco.Numero}, {endereco.Bairro}, {endereco.Cidade}, {endereco.Uf}, {endereco.Cep}, Brasil";
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "v1/geocode/search?format=json&limit=5&filter=countrycode:br&text=" + Uri.EscapeDataString(texto)
            + "&apiKey=" + Uri.EscapeDataString(apiKey));
        try
        {
            using var resposta = await http.SendAsync(req, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                // A URL contém a chave e o endereço: não deve ser registrada nos logs.
                logger.LogWarning("Geocodificação respondeu HTTP {StatusCode}.", (int)resposta.StatusCode);
                throw new ValidationException("Não foi possível localizar o endereço agora. Tente novamente em instantes.");
            }
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("results", out var resultados)
                && resultados.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in resultados.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object || Texto(r, "result_type") != "building"
                        || Texto(r, "country_code") != "br" || Texto(r, "state_code") != endereco.Uf
                        || Rua(Texto(r, "street")) != Rua(endereco.Logradouro)
                        || Normalizar(Texto(r, "housenumber")) != Normalizar(endereco.Numero)
                        || Normalizar(Texto(r, "city")) != Normalizar(endereco.Cidade)
                        || !r.TryGetProperty("rank", out var rank) || rank.ValueKind != JsonValueKind.Object
                        || !Numero(rank, "confidence_building_level", out var precisao) || precisao < 0.7)
                        continue;

                    var cep = Normalizar(Texto(r, "postcode"));
                    var cepInformado = Normalizar(endereco.Cep ?? "");
                    if (cepInformado.Length >= 5 && cep.Length >= 5)
                    {
                        if (cep[..5] != cepInformado[..5]) continue;
                    }
                    else if (Normalizar(Texto(r, "suburb")) != Normalizar(endereco.Bairro)
                        && Normalizar(Texto(r, "district")) != Normalizar(endereco.Bairro)) continue;

                    if (!Numero(r, "lon", out var longitude) || !Numero(r, "lat", out var latitude)
                        || !double.IsFinite(longitude) || !double.IsFinite(latitude)
                        || longitude is < -180 or > 180 || latitude is < -90 or > 90) continue;
                    var ponto = new PontoEntrega(longitude, latitude);
                    cache.Set(chave, ponto, TimeSpan.FromHours(1));
                    return ponto;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ValidationException("Não foi possível consultar o endereço. Tente novamente em instantes.");
        }
        // Não transforma resultados de bairro, CEP ou cidade em um endereço preciso.
        throw new ValidationException("Não conseguimos localizar esse endereço com segurança. Confira o CEP, a rua e o número.");
    }
}
