using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioAtualizarPerfilDTO
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome deve possuir no máximo 100 caracteres.")]
        public string Nome { get; set; }
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [MaxLength(150, ErrorMessage = "O e-mail deve possuir no máximo 150 caracteres.")]
        public string Email { get; set; }
    }
}
