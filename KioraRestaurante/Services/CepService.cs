using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KioraRestaurante.DTOs.Endereco;
using Microsoft.Extensions.Caching.Memory;

namespace KioraRestaurante.Services;

public class CepService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public CepService(HttpClient http, IMemoryCache cache, IConfiguration configuration)
    {
        _http = http;
        _cache = cache;
        _configuration = configuration;
    }

    private static string Normalizar(string cep)
    {
        cep = cep.Trim();

        // Aceita 01001000 ou 01001-000.
        if (!Regex.IsMatch(cep, @"^[0-9]{5}-?[0-9]{3}$"))
        {
            throw new ValidationException(
                "Informe um CEP com oito números.");
        }

        return cep.Replace("-", "");
    }

    public async Task<CepResponseDTO> Consultar(string cep)
    {
        var normalizado = Normalizar(cep);
        var chave = $"cep:{normalizado}";

        if (_cache.TryGetValue<CepResponseDTO>(chave, out var salvo)
            && salvo != null)
        {
            return salvo;
        }

        RetornoViaCep? retorno;

        try
        {
            retorno = await _http.GetFromJsonAsync<RetornoViaCep>(
                $"{normalizado}/json/");
        }
        catch (HttpRequestException)
        {
            throw new ValidationException(
                "Não foi possível consultar o CEP. Tente novamente.");
        }
        catch (OperationCanceledException)
        {
            throw new ValidationException(
                "A consulta do CEP demorou demais. Tente novamente.");
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "O serviço de CEP retornou uma resposta inválida.");
        }

        if (retorno == null || string.Equals(
                retorno.Erro.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("CEP não encontrado.");
        }

        if (string.IsNullOrWhiteSpace(retorno.Cidade)
            || string.IsNullOrWhiteSpace(retorno.Uf)
            || string.IsNullOrWhiteSpace(retorno.Ibge))
        {
            throw new ValidationException(
                "Não foi possível identificar o município desse CEP.");
        }

        var resultado = new CepResponseDTO
        {
            Cep = normalizado.Insert(5, "-"),
            Logradouro = retorno.Logradouro ?? string.Empty,
            Bairro = retorno.Bairro ?? string.Empty,
            Cidade = retorno.Cidade,
            Uf = retorno.Uf,
            Ibge = retorno.Ibge
        };

        // Guarda apenas dados públicos do CEP.
        _cache.Set(chave, resultado, TimeSpan.FromHours(6));

        return resultado;
    }

    public void ExigirAreaAtendida(string uf, string ibge)
    {
        var municipios = _configuration
            .GetSection("Entrega:MunicipiosIbge")
            .Get<string[]>() ?? Array.Empty<string>();

        if (uf != "SP" || !municipios.Contains(ibge))
        {
            throw new ValidationException(
                "Esse endereço está fora da área de entrega do restaurante.");
        }
    }

    // Representa o formato enviado pelo ViaCEP.
    public class RetornoViaCep
    {
        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Cidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("ibge")]
        public string? Ibge { get; set; }

        [JsonPropertyName("erro")]
        public JsonElement Erro { get; set; }
    }
}