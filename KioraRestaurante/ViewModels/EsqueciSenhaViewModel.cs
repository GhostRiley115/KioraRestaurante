using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.ViewModels
{
    // ================================================================
    // VIEWMODEL DE RECUPERAÇÃO DE SENHA
    // ================================================================

    // Esta classe representa os dados necessários
    // para iniciar o processo de recuperação de senha.
    //
    // Neste primeiro momento, o usuário precisará
    // informar somente o e-mail cadastrado.
    public class EsqueciSenhaViewModel
    {
        // ================================================================
        // E-MAIL
        // ================================================================

        // Define que o preenchimento do e-mail é obrigatório.
        [Required(
            ErrorMessage = "Informe seu e-mail."
        )]

        // Verifica se o valor informado possui
        // um formato válido de e-mail.
        [EmailAddress(
            ErrorMessage = "Digite um e-mail válido."
        )]

        // Guarda o e-mail informado pelo usuário.
        public string Email { get; set; } = null!;
    }
}