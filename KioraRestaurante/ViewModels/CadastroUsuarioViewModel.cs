// Permite utilizar os atributos de validação, como Required, EmailAddress, MinLength e Compare.
using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.ViewModels
{
    // ViewModel utilizado exclusivamente para receber os dados enviados pelo formulário de cadastro.
    // Ele é diferente da Model Usuario porque representa os dados necessários para o formulário.    
    // O campo ConfirmarSenha, por exemplo, é necessário para validar o cadastro, mas não precisa ser salvo
    // na tabela Usuarios do banco de dados.
    public class CadastroUsuarioViewModel
    {
        // NOME
        
        // [Required] informa que o campo é obrigatório.
        
        // Caso o usuário tente cadastrar sem informar o nome, o ModelState será considerado inválido.
        [Required(ErrorMessage = "O nome é obrigatório.")]
        public string Nome { get; set; }

        // E-MAIL

        // O e-mail também é obrigatório.
        [Required(ErrorMessage = "O e-mail é obrigatório.")]

        // [EmailAddress] verifica se o valor informado possui um formato válido de e-mail.
        
        // Exemplo válido: usuario@email.com
        
        // Exemplo inválido: usuario
        [EmailAddress(ErrorMessage = "Digite um e-mail válido.")]
        public string Email { get; set; }


        // SENHA
        

        // A senha não pode ficar vazia.
        [Required(ErrorMessage = "A senha é obrigatória.")]

        // Define o tamanho mínimo da senha.
       
        // Neste caso, a senha precisa possuir pelo menos 6 caracteres.
        [MinLength(6, ErrorMessage = "A senha deve possuir pelo menos 6 caracteres.")]
        public string Senha { get; set; }


        // CONFIRMAÇÃO DA SENHA
        

        // O usuário precisa preencher a confirmação da senha.
        [Required(ErrorMessage = "Confirme sua senha.")]

        // [Compare("Senha")] compara este campo com a propriedade Senha.
        
        // Se os valores forem diferentes, o cadastro será considerado inválido.
        
        // Exemplo:        
        // Senha:          123456
        // ConfirmarSenha: 123456        
        // Resultado: válido.
        // 
        // Senha:          123456
        // ConfirmarSenha: 123457        
        // Resultado: inválido.
        [Compare("Senha", ErrorMessage = "As senhas não são iguais.")]
        public string ConfirmarSenha { get; set; }
    }
}