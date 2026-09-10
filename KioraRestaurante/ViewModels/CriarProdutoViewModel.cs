using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KioraRestaurante.ViewModels
{
    public class CriarProdutoViewModel
    {
        //Caso algo esteja errado, a messagem de erro aparece antes mesmo de alguma informação chegar ao banco

        [Required(ErrorMessage = "O nome é Obrigatório.")]
        [MaxLength(100)]
        public string Nome { get; set; } = null!;

        [MaxLength(500)]
        // O "?" deixa o campo como opcional
        public string? Descricao { get; set; }

        [Required(ErrorMessage = "Informe o preço.")]
        // O Range garante que o valor digitado não fuja do esperado
        [Range(typeof(decimal), "0.01", "9999999", ErrorMessage = "Preço inválido")]
        public decimal Preco { get; set; }

        public string? Imagem { get; set; }

        [Required(ErrorMessage = "Selecione uma categoria.")]
        public int CategoriId { get; set; }

        //È a parte onde o formulário do HTMl vai enviar no select do dropdonw
        public List<SelectListItem> CategoriasDisponiveis { get; set; } = new();
    }
}
