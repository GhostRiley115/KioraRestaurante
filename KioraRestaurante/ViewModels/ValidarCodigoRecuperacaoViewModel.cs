using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.ViewModels
{
    public class ValidarCodigoRecuperacaoViewModel
    {
        [Required(ErrorMessage = "Informe o código de recuperação.")]
        [StringLength(
            6,
            MinimumLength = 6,
            ErrorMessage = "O código deve possuir 6 dígitos.")]
        public string Codigo { get; set; } = string.Empty;
    }
}