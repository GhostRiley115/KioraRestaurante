using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Validacoes;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioRedefinirSenhaDTO
    {
        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(150, ErrorMessage = "O e-mail deve ter no máximo 150 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O token é obrigatório.")]
        [RegularExpression(@"[0-9]{6}", ErrorMessage = "Informe um código de 6 dígitos.")]
        public string Token { get; set; } = string.Empty;


        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [MinLength(RegrasSenha.TamanhoMinimo, ErrorMessage = RegrasSenha.Mensagem)]
        [RegularExpression(RegrasSenha.Padrao, ErrorMessage = RegrasSenha.Mensagem)]
        public string NovaSenha { get; set; } = string.Empty;


        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(nameof(NovaSenha), ErrorMessage = "As novas senhas não coincidem.")]
        public string ConfirmarNovaSenha { get; set; } = string.Empty;
    }
}
