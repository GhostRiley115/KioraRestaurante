using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioRedefinirSenhaDTO
    {
        [Required(ErrorMessage = "O token é obrigatório.")]
        public string Token { get; set; } = string.Empty;


        [Required(ErrorMessage = "A nova senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A nova senha deve possuir pelo menos 6 caracteres.")]
        public string NovaSenha { get; set; } = string.Empty;


        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(nameof(NovaSenha), ErrorMessage = "As novas senhas não coincidem.")]
        public string ConfirmarNovaSenha { get; set; } = string.Empty;
    }
}
