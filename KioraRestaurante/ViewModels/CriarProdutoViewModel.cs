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
        public decimal Preco { get; set; }

        [MaxLength(500)]
        public string? Imagem { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        public int CategoriaId { get; set; }

        public List<SelectListItem> CategoriasDisponiveis { get; set; } = new();
    }
}
