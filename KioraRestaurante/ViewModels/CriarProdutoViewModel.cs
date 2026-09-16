using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KioraRestaurante.ViewModels
{
    public class CriarProdutoViewModel
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100)]
        public string Nome { get; set; } = null!;

        [MaxLength(500)]
        public string? Descricao { get; set; }
        [Required(ErrorMessage = "Informe o preço.")]
        [Range(typeof(decimal), "0.01", "9999999", ErrorMessage = "Preço inválido.")]

        [MaxLength(500)]
        public string? Imagem { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        public int Categoria { get; set; }

        public List<SelectListItem> CategoriaDisponiveis { get; set; } = new();
    }
}
