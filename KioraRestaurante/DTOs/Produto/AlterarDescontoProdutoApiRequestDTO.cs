using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto;

// A API distingue a remoção explícita (zero) da ausência do campo no JSON.
// O formulário web mantém seu contrato de desconto já existente.
public class AlterarDescontoProdutoApiRequestDTO
{
    [Required(ErrorMessage = "Informe o percentual de desconto. Use zero para removê-lo.")]
    [Range(typeof(decimal), "0", "90", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Informe um desconto entre 0% e 90%.")]
    public decimal? Percentual { get; set; }
}
