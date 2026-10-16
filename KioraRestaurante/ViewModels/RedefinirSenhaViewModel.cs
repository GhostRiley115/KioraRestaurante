using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Validacoes;

namespace KioraRestaurante.ViewModels
{
    // ViewModel utilizada na etapa final da recuperação de senha.
    public class RedefinirSenhaViewModel
    {
        // Código de recuperação recebido pelo usuário por e-mail.
        [Required(ErrorMessage = "Informe o código de recuperação.")]
        [StringLength(
            6,
            MinimumLength = 6,
            ErrorMessage = "O código deve possuir 6 dígitos.")]
        public string Codigo { get; set; } = string.Empty;


        // Nova senha escolhida pelo usuário.
        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [MinLength(
            RegrasSenha.TamanhoMinimo,
            ErrorMessage = RegrasSenha.Mensagem)]
        [RegularExpression(
            RegrasSenha.Padrao,
            ErrorMessage = RegrasSenha.Mensagem)]
        public string NovaSenha { get; set; } = string.Empty;


        // Confirmação da nova senha.
        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(
            nameof(NovaSenha),
            ErrorMessage = "As novas senhas não coincidem.")]
        public string ConfirmarNovaSenha { get; set; } = string.Empty;
    }
}
