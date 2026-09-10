using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioSolicitarRecuperacaoDTO
    {
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Required(ErrorMessage = "O e - mail é obrigatório.")]
        public string Email { get; set; } = string.Empty;
    }
}
