using System.ComponentModel.DataAnnotations;
namespace KioraRestaurante.Models.Salao;
public class ConviteFuncionario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int CriadoPorId { get; set; }
    [MaxLength(64)] public string TokenHash { get; set; } = "";
    public Guid VersaoSessao { get; set; }
    public DateTime CriadoEmUtc { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? UsadoEmUtc { get; set; }
}
