using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioResponseDTO
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public TipoUsuario Tipo {  get; set; }
        public bool Ativo { get; set; }
    }
}
