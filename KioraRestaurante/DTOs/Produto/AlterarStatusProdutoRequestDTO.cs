using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto
{
    public class AlterarStatusProdutoRequestDTO
    {
        [Required(ErrorMessage = "Informe se o produto deve ficar ativo.")]
        public bool? Ativo { get; set; }
    }
}
