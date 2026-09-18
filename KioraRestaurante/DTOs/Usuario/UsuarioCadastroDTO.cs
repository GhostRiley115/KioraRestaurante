using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Validacoes;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioCadastroDTO
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome deve possuir no máximo 100 caracteres.")]
        public string Nome { get; set; } = string.Empty;


        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        //Verifica se email possuiu @ 
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [MaxLength(150, ErrorMessage = "O e-mail deve possuir no máximo 150 caracteres.")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "A senha é obrigatória.")]
        [MinLength(RegrasSenha.TamanhoMinimo, ErrorMessage = RegrasSenha.Mensagem)]
        [RegularExpression(RegrasSenha.Padrao, ErrorMessage = RegrasSenha.Mensagem)]
        public string Senha { get; set; } = string.Empty;


        [Required(ErrorMessage = "A confirmação da senha é obrigatória.")]
        [Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem.")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }
}
