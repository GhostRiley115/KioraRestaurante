using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.ViewModels.Admin;

public class FiltroPedidosAdmin : IValidatableObject
{
    [StringLength(150)] public string? Busca { get; set; }
    [Range(1, int.MaxValue)] public int? PedidoId { get; set; }
    [Range(1, int.MaxValue)] public int? UsuarioId { get; set; }
    [EnumDataType(typeof(StatusPedido))] public StatusPedido? Status { get; set; }
    [EnumDataType(typeof(StatusPagamento))] public StatusPagamento? Pagamento { get; set; }
    [DataType(DataType.Date)] public DateTime? Inicio { get; set; }
    [DataType(DataType.Date)] public DateTime? Fim { get; set; }
    [Range(1, int.MaxValue)] public int Pagina { get; set; } = 1;

    // Evita intervalos invertidos e datas fora do intervalo útil deste sistema.
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Inicio.HasValue && (Inicio.Value.Year < 2000 || Inicio.Value.Year > 2100)
            || Fim.HasValue && (Fim.Value.Year < 2000 || Fim.Value.Year > 2100))
            yield return new ValidationResult("Informe datas entre 2000 e 2100.");
        if (Inicio.HasValue && Fim.HasValue && Inicio.Value.Date > Fim.Value.Date)
            yield return new ValidationResult("A data inicial não pode ser posterior à data final.");
    }
}
