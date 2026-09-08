// Permite utilizar os recursos de validação
// fornecidos pelo ASP.NET Core.
using System.ComponentModel.DataAnnotations;


namespace KioraRestaurante.ViewModels
{
    // ================================================================
    // VIEWMODEL DE EDIÇÃO DO PERFIL
    // ================================================================

    // Esta classe representa os dados que poderão
    // ser alterados pelo usuário dentro do seu perfil.
    //
    // Utilizamos um ViewModel separado da classe Usuario
    // para evitar que informações internas do usuário,
    // como senha, tipo de usuário e tokens,
    // sejam enviadas pelo formulário.
    public class EditarPerfilViewModel
    {
        // ================================================================
        // NOME
        // ================================================================

        // Define que o nome é obrigatório.
        //
        // Caso o usuário tente salvar o formulário
        // sem informar o nome, esta mensagem será exibida.
        [Required(ErrorMessage = "O nome é obrigatório.")]

        // Guarda o novo nome informado pelo usuário.
        public string Nome { get; set; } = null!;


        // ================================================================
        // E-MAIL
        // ================================================================

        // Define que o e-mail é obrigatório.
        [Required(ErrorMessage = "O e-mail é obrigatório.")]

        // Verifica se o valor informado possui
        // um formato válido de endereço de e-mail.
        [EmailAddress(ErrorMessage = "Digite um e-mail válido.")]

        // Guarda o novo e-mail informado pelo usuário.
        public string Email { get; set; } = null!;
    }
}