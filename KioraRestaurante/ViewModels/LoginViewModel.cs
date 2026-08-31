// Permite utilizar os recursos de validação do ASP.NET Core.
using System.ComponentModel.DataAnnotations;


namespace KioraRestaurante.ViewModels
{
    // ViewModel responsável por receber os dados enviados pelo formulário de login.
    
    // Ele separa os dados do formulário da entidade Usuario.
    public class LoginViewModel
    {
        // ================================================================
        // E-MAIL
        // ================================================================

        // Define que o campo de e-mail é obrigatório.
        [Required(ErrorMessage = "Informe seu e-mail.")]

        // Verifica se o valor informado possui formato de e-mail válido.
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]

        // Armazena o e-mail informado pelo usuário.
        public string Email { get; set; }


        // ================================================================
        // SENHA
        // ================================================================

        // Define que a senha é obrigatória.
        [Required(ErrorMessage = "Informe sua senha.")]

        // Armazena a senha digitada pelo usuário.
        //
        // A senha será enviada ao Controller apenas para
        // ser verificada pelo UsuarioServices.
        public string Senha { get; set; }
    }
}

