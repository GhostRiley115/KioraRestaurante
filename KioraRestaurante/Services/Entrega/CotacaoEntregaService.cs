using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Models;
using KioraRestaurante.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services.Entrega;

public record CotacaoEntrega(int DistanciaMetros, decimal Taxa, string Token, DateTime ExpiraEmUtc)
{
    public int DuracaoPercursoSegundos { get; init; }
    public int PreparoMinutos => 30;
    public int PercursoMinutos => (int)Math.Ceiling(DuracaoPercursoSegundos / 60m);
    public int PrazoTotalMinutos => PreparoMinutos + PercursoMinutos;
}

public class CotacaoEntregaService(IRotasEntrega rotas, IOptions<EntregaOptions> options,
    IDataProtectionProvider protecao, TimeProvider relogio)
{
    private readonly IDataProtector _protetor = protecao.CreateProtector("Kiora.Entrega.Cotacao.v2");
    private sealed record CotacaoProtegida(int UsuarioId, string EnderecoHash, string PoliticaHash,
        int DistanciaMetros, decimal Taxa, DateTime ExpiraEmUtc, int DuracaoPercursoSegundos);

    private static string Hash(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    private static string EnderecoHash(EnderecoEntrega e) => Hash(JsonSerializer.Serialize(new
    {
        e.Cep, e.Logradouro, e.Numero, e.Bairro, e.Cidade, e.Uf, e.Complemento, e.Referencia
    }));

    private string PoliticaHash() => Hash(JsonSerializer.Serialize(new
    {
        options.Value.Restaurante, options.Value.LimiteMetros, options.Value.Faixas
    }));

    public decimal CalcularTaxa(decimal metros)
    {
        var o = options.Value;
        if (metros < 0 || metros > o.LimiteMetros)
            throw new ValidationException("Esse endereço está fora da área de entrega de 35 km pelas ruas.");

        return o.Faixas.OrderBy(f => f.AteMetros).FirstOrDefault(f => metros <= f.AteMetros)?.Valor
            ?? throw new ValidationException("Não foi possível calcular a taxa de entrega. Tente novamente.");
    }

    public async Task<CotacaoEntrega> Cotar(int usuarioId, EnderecoEntrega endereco, CancellationToken ct = default)
    {
        var rota = await rotas.Calcular(endereco, ct);
        var metros = rota.DistanciaMetros;
        if (rota.DuracaoSegundos is < 0 or > 86400)
            throw new ValidationException("Não foi possível calcular o tempo estimado da entrega.");
        var taxa = CalcularTaxa(metros);
        var distancia = checked((int)Math.Ceiling(metros));
        var expira = relogio.GetUtcNow().UtcDateTime.AddMinutes(15);
        var dados = new CotacaoProtegida(usuarioId, EnderecoHash(endereco), PoliticaHash(), distancia, taxa, expira, rota.DuracaoSegundos);
        return new(distancia, taxa, _protetor.Protect(JsonSerializer.Serialize(dados)), expira) { DuracaoPercursoSegundos = rota.DuracaoSegundos };
    }

    public CotacaoEntrega Conferir(int usuarioId, EnderecoEntrega endereco, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("Calcule novamente a entrega antes de confirmar.");
        CotacaoProtegida? dados;
        try
        {
            dados = JsonSerializer.Deserialize<CotacaoProtegida>(_protetor.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        {
            throw new ValidationException("Calcule novamente a entrega antes de confirmar.");
        }

        // Usuário, endereço e tabela são conferidos no servidor; valores do navegador não são utilizados.
        if (dados == null || dados.UsuarioId != usuarioId || dados.EnderecoHash != EnderecoHash(endereco)
            || dados.PoliticaHash != PoliticaHash() || dados.ExpiraEmUtc <= relogio.GetUtcNow().UtcDateTime
            || dados.DuracaoPercursoSegundos is < 0 or > 86400
            || dados.Taxa != CalcularTaxa(dados.DistanciaMetros))
            throw new ValidationException("O endereço ou a cotação mudou. Calcule novamente a entrega.");

        return new(dados.DistanciaMetros, dados.Taxa, token, dados.ExpiraEmUtc) { DuracaoPercursoSegundos = dados.DuracaoPercursoSegundos };
    }
}
