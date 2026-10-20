using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.ViewModels
{
    public class ValidarCodigoRecuperacaoViewModel
    {
        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(150, ErrorMessage = "O e-mail deve ter no máximo 150 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o código de recuperação.")]
        [StringLength(
            6,
            MinimumLength = 6,
            ErrorMessage = "O código deve possuir 6 dígitos.")]
        [RegularExpression(@"[0-9]{6}", ErrorMessage = "Informe um código de 6 dígitos.")]
        public string Codigo { get; set; } = string.Empty;
    }
}