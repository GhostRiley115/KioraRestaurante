namespace KioraRestaurante.Services.Entrega;

public record EnderecoRota(string Logradouro, string Numero, string Bairro, string Cidade, string Uf, string? Cep = null);
public record PontoEntrega(double Longitude, double Latitude);

public interface IGeocodificadorEntrega
{
    Task<PontoEntrega> Localizar(EnderecoRota endereco, CancellationToken ct = default);
}
