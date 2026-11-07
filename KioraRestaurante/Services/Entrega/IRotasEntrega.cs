using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Entrega;

public record RotaEntrega(decimal DistanciaMetros, int DuracaoSegundos);

public interface IRotasEntrega
{
    Task<RotaEntrega> Calcular(EnderecoEntrega destino, CancellationToken ct = default);
    async Task<decimal> CalcularMetros(EnderecoEntrega destino, CancellationToken ct = default) =>
        (await Calcular(destino, ct)).DistanciaMetros;
}
