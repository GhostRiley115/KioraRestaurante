using System.ComponentModel.DataAnnotations.Schema;
using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;

        public StatusPedido Status { get; set; } = StatusPedido.Recebido;
        public FormaPagamento FormaPagamento { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ValorTotal { get; set; }

        public List<ItemPedido> ItensPedido { get; set; } = new();
    }
}
