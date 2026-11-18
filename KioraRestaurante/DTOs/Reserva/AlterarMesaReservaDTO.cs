using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Salao;

namespace KioraRestaurante.DTOs.Reserva;

public class AlterarMesaReservaDTO : ComandoSalaoDTO, IValidatableObject
{
    [Required(ErrorMessage = "Informe se a mesa deve ficar habilitada.")]
    public bool? Ativa { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Versao == Guid.Empty)
            yield return new ValidationResult("Consulte a versão atual da mesa antes de alterar.", [nameof(Versao)]);
        if (ChaveSolicitacao == Guid.Empty)
            yield return new ValidationResult("Informe uma chave para esta solicitação.", [nameof(ChaveSolicitacao)]);
    }
}
