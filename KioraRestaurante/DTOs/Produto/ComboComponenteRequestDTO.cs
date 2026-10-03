using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto;

public class ComboComponenteRequestDTO
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Selecione um produto para o componente.")]
    public int ProdutoId { get; set; }

    [Range(1, 30,
        ErrorMessage = "A quantidade do componente deve ser de 1 a 30.")]
    public int Quantidade { get; set; }
}