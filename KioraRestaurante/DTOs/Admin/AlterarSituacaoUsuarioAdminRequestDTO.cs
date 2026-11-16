using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Admin;

// Recebe a situação desejada para a conta do cliente.
public class AlterarSituacaoUsuarioAdminRequestDTO
{
    [Required]
    public bool? Ativo { get; set; }
}