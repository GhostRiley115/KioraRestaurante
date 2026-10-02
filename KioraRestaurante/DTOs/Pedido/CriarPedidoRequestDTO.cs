using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.DTOs.Pedido;

public class CriarPedidoRequestDTO
{
    [Required(ErrorMessage = "Atualize a revisão do pedido.")]
    public string TokenRevisao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha a forma de pagamento.")]
    [EnumDataType(typeof(FormaPagamento))]
    public FormaPagamento? FormaPagamento { get; set; }

    [Required(ErrorMessage = "Escolha um endereço.")]
    [Range(1, int.MaxValue)]
    public int? EnderecoId { get; set; }

    [Required(ErrorMessage = "Atualize a revisão do endereço.")]
    public Guid? EnderecoVersao { get; set; }
}