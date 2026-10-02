using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.DTOs.Admin;

public class PedidoAdminDTO
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string NomeCliente { get; set; } = "";
    public string EmailCliente { get; set; } = "";
    public DateTime DataPedido { get; set; }
    public decimal ValorTotal { get; set; }
    public StatusPedido StatusPedido { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
}
