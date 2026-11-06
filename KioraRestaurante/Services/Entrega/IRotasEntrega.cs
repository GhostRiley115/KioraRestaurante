using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Entrega;

public interface IRotasEntrega
{
    Task<decimal> CalcularMetros(EnderecoEntrega destino, CancellationToken ct = default);
}
