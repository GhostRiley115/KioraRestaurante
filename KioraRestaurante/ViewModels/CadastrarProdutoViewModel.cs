using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Categoria;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class CadastrarProdutoViewModel
{
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

    public bool Disponivel { get; set; } = true;

    [Required(ErrorMessage = "Selecione uma foto.")]
    public IFormFile? Foto { get; set; }

    // Essa lista é preenchida pelo servidor para montar o select.
    [BindNever]
    [ValidateNever]
    public List<CategoriaResponseDTO> Categorias { get; set; } = new();
}