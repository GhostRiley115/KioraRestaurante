// Permite utilizar os recursos de validação
// fornecidos pelo ASP.NET Core.
using System.ComponentModel.DataAnnotations;


namespace KioraRestaurante.ViewModels
{
    // ================================================================
    // VIEWMODEL DE ALTERAÇÃO DE SENHA
    // ================================================================

    // Esta classe representa os dados necessários
    // para que o usuário possa alterar sua senha.
    //
    // Utilizamos um ViewModel separado para não utilizar
    // diretamente a entidade Usuario no formulário.
    public class AlterarSenhaViewModel
    {
        // ================================================================
        // SENHA ATUAL
        // ================================================================

        // Define que a senha atual é obrigatória.
        [Required(ErrorMessage = "Informe sua senha atual.")]

        // Guarda a senha atualmente utilizada pelo usuário.
        public string SenhaAtual { get; set; } = null!;


        // ================================================================
        // NOVA SENHA
        // ================================================================

        // Define que a nova senha é obrigatória.
        [Required(ErrorMessage = "Informe a nova senha.")]

        // Define uma quantidade mínima de caracteres
        // para a nova senha.
        [MinLength(
            6,
            ErrorMessage = "A nova senha deve possuir pelo menos 6 caracteres."
        )]

        // Guarda a nova senha escolhida pelo usuário.
        public string NovaSenha { get; set; } = null!;


        // ================================================================
        // CONFIRMAÇÃO DA NOVA SENHA
        // ================================================================

        // Define que a confirmação da senha é obrigatória.
        [Required(ErrorMessage = "Confirme sua nova senha.")]

        // Compara este campo com o campo NovaSenha.
        //
        // Caso os dois valores sejam diferentes,
        // será exibida a mensagem informada abaixo.
        [Compare(
            "NovaSenha",
            ErrorMessage = "As senhas não coincidem."
        )]

        // Guarda a confirmação da nova senha.
        public string ConfirmarNovaSenha { get; set; } = null!;
    }
}