using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.DTOs.Pedido;

public class CriarPedidoRequestDTO : CotarEntregaRequestDTO
{
    [Required(ErrorMessage = "Atualize a revisão do pedido.")]
    public string TokenRevisao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha a forma de pagamento.")]
    [EnumDataType(typeof(FormaPagamento))]
    public FormaPagamento? FormaPagamento { get; set; }

    [Required(ErrorMessage = "Calcule a entrega antes de confirmar o pedido.")]
    public string TokenEntrega { get; set; } = "";

    public Dictionary<int, string?> Observacoes { get; set; } = [];

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var erro in base.Validate(validationContext)) yield return erro;
        if (Observacoes == null)
            yield return new("Confira as observações dos produtos.", [nameof(Observacoes)]);
        else foreach (var item in Observacoes)
            if (item.Key <= 0 || item.Value?.Length > 500)
                yield return new("A observação de cada produto deve ter até 500 caracteres.", [$"Observacoes[{item.Key}]"]);
    }
}
