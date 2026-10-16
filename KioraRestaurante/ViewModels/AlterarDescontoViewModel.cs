using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class AlterarDescontoViewModel
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o percentual de desconto.")]
    [RegularExpression(
        @"^[0-9]{1,2}([,.][0-9]{1,2})?$",
        ErrorMessage = "Informe um percentual como 10,50, sem o símbolo %.")]
    public string PercentualTexto { get; set; } = "0";

    // Informações somente para exibição.
    [BindNever]
    [ValidateNever]
    public string NomeProduto { get; set; } = string.Empty;

    [BindNever]
    [ValidateNever]
    public decimal PrecoOriginal { get; set; }
}