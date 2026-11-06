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
}
