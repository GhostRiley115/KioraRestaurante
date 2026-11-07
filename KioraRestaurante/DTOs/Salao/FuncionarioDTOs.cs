using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.DTOs.Salao;
public class SalvarFuncionarioDTO
{
    public int? Id { get; set; }
    public Guid Versao { get; set; }
    [Required,StringLength(100,MinimumLength=2)] public string Nome { get; set; } = "";
    [Required,EmailAddress,StringLength(150)] public string Email { get; set; } = "";
    [StringLength(25)] public string? Telefone { get; set; }
    [EnumDataType(typeof(TipoUsuario))] public TipoUsuario Cargo { get; set; } = TipoUsuario.Garcom;
    public bool Ativo { get; set; } = true;
}
public class AceitarConviteDTO
{
    [Required,StringLength(100)] public string Token { get; set; } = "";
    [Required,StringLength(128,MinimumLength=8,ErrorMessage="Use uma senha de 8 a 128 caracteres.")] public string Senha { get; set; } = "";
    [Compare(nameof(Senha),ErrorMessage="As senhas devem ser iguais.")] public string ConfirmarSenha { get; set; } = "";
}
public record FuncionarioDTO(int Id,string Nome,string Email,string? Telefone,TipoUsuario Cargo,bool Ativo,bool AguardandoConvite,Guid Versao);
public record ResultadoFuncionario(int Id,bool ConviteEnviado);
