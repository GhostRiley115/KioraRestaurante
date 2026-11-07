using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Pedido;

public class AlterarEnderecoPedidoRequestDTO : CotarEntregaRequestDTO
{
    [Range(1, int.MaxValue)] public int PedidoId { get; set; }
    public Guid VersaoPedido { get; set; }
    [Required(ErrorMessage = "Calcule a nova entrega antes de confirmar.")]
    public string TokenEntrega { get; set; } = "";
}

public class CancelarPedidoRequestDTO
{
    [Range(1, int.MaxValue)] public int PedidoId { get; set; }
    public Guid VersaoPedido { get; set; }
    [StringLength(300, ErrorMessage = "O motivo deve ter até 300 caracteres.")]
    public string? Motivo { get; set; }
}
