using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Validacoes;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioRedefinirSenhaDTO
    {
        [Required(ErrorMessage = "O token é obrigatório.")]
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
