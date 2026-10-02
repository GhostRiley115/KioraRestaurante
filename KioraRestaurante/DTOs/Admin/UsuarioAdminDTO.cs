using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.DTOs.Admin;

// Nunca enviamos hash de senha ou tokens de recuperação para a tela administrativa.
public class UsuarioAdminDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Ativo { get; set; }
    public TipoUsuario Tipo { get; set; }
    public int QuantidadePedidos { get; set; }
}
