using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioSolicitarRecuperacaoDTO
    {
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Required(ErrorMessage = "O e - mail é obrigatório.")]
        [StringLength(150, ErrorMessage = "O e-mail deve ter no máximo 150 caracteres.")]
        public string Email { get; set; } = string.Empty;
    }
}
