using System.ComponentModel.DataAnnotations;
namespace KioraRestaurante.ViewModels.Admin;

// Recebe somente o ID e a situação desejada: não aceita nome, senha ou perfil.
public class AlterarSituacaoUsuarioRequest
{
    [Range(1, int.MaxValue)] public int UsuarioId { get; set; }
    [Required] public bool? Ativo { get; set; }
    public string? Retorno { get; set; }
}
