using System.ComponentModel.DataAnnotations;
namespace KioraRestaurante.ViewModels.Admin;

public class FiltroRelatorioAdmin : IValidatableObject
{
    public DateTime? Inicio { get; set; }
    public DateTime? Fim { get; set; }
    public DateTime? CompararInicio { get; set; }
    public DateTime? CompararFim { get; set; }
    [Required(ErrorMessage="Escolha um canal de venda.")]
    [RegularExpression("^(todos|delivery|salao)$", ErrorMessage="Escolha um canal válido.")]
    public string Canal { get; set; } = "todos";
    [Range(1, int.MaxValue)] public int Pagina { get; set; } = 1;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var (inicio, fim) in new[] { (Inicio, Fim), (CompararInicio, CompararFim) })
        {
            if (inicio.HasValue != fim.HasValue) yield return new("Informe o início e o fim de cada período.");
            if (inicio is {} a && fim is {} b && (a.Year < 2000 || b.Year > 2100 || a.Date > b.Date || (b.Date-a.Date).TotalDays > 365))
                yield return new("Cada período deve ter até 366 dias, entre 2000 e 2100, com início anterior ao fim.");
        }
    }
}
