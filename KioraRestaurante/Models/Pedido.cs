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

        public EnderecoEntrega EnderecoEntrega { get; set; } = null!;

        public FormaPagamento FormaPagamento { get; set; }
        public StatusPedido StatusPedido { get; set; } = StatusPedido.Recebido;
        public StatusPagamento StatusPagamento { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ValorTotal { get; set; }

        public List<ItemPedido> ItensPedido { get; set; } = new();
    }
}
