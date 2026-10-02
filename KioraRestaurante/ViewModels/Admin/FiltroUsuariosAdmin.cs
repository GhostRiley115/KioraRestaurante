using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.ViewModels.Admin;

// Campos aceitos na busca. Nenhum deles permite modificar o perfil de um usuário.
public class FiltroUsuariosAdmin
{
    [StringLength(150)] public string? Busca { get; set; }
    public bool? Ativo { get; set; }
    [EnumDataType(typeof(TipoUsuario))] public TipoUsuario? Tipo { get; set; }
    [Range(1, int.MaxValue)] public int Pagina { get; set; } = 1;
}
