using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Carrinho;

public class MesclarCarrinhoRequestDTO
{
    //Chave: ID do produto que apresentou conflito.
    //Valor: quantidade final escolhida pelo cliente.
    [Required]
    public Dictionary<int, int> QuantidadesResolvidas { get; set; } = new();
}