using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto
{
    public class AlterarDisponibilidadeProdutoRequestDTO
    {
        [Required(ErrorMessage = "Informe se o produto está disponível.")]
        public bool? Disponivel { get; set; }
    }
}
