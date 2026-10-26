using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models;

public class LimpezaImagemPendente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string PublicId { get; set; } = string.Empty;

    // Uma proteção adicional para referências antigas sem PublicId.
    [MaxLength(500)]
    public string? Url { get; set; }

    public DateTime ProximaTentativaUtc { get; set; }
    public int Tentativas { get; set; }
}