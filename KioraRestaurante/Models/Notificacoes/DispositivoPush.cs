using System;
using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models;

namespace KioraRestaurante.Models.Notificacoes;

public sealed class DispositivoPush
{
    public long Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(4096)]
    public string TokenFcm { get; set; } = string.Empty;

    public int? UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    [MaxLength(64)]
    public string? VisitanteHash { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEmUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime AtualizadoEmUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? DesativadoEmUtc { get; set; }
}