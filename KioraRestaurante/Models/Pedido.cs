using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; }
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;

        public StatusPedido Status { get; set; } = StatusPedido.Recebido;
        public FormaPagamento FormaPagamento { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ValorTotal { get; set; }
        public List<ItemPedido> Itens { get; set; } = new();
    }

    public enum StatusPedido
    {
        Recebido,
        EmPreparo,
        SaiuParaEntrega,
        Entregue,
        Cancelado
    }
    public enum FormaPagamento
    {
        Dinheiro,
        Pix,
        CartaoCredito,
        CartaoDebito
    }
}
