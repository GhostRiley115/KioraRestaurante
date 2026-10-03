using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto;

public class AlterarDescontoRequestDTO
{
    [Range(typeof(decimal), "0", "90",
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Informe um desconto entre 0% e 90%.")]
    public decimal Percentual { get; set; }
}