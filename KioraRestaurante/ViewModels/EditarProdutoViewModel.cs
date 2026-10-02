using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Categoria;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class EditarProdutoViewModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição.")]
    [StringLength(500)]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o preço.")]
    [RegularExpression(@"^[0-9]{1,8}([,.][0-9]{1,2})?$",
        ErrorMessage = "Informe um preço como 39,90, sem separador de milhares.")]
    public string PrecoTexto { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria.")]
    public int CategoriaId { get; set; }

    // Na edição, não enviar uma nova foto significa manter a atual.
    public IFormFile? Foto { get; set; }

    // Esses campos são preenchidos pelo servidor, não pelo formulário.
    [BindNever]
    [ValidateNever]
    public string ImagemAtual { get; set; } = string.Empty;

    [BindNever]
    [ValidateNever]
    public List<CategoriaResponseDTO> Categorias { get; set; } = new();
}