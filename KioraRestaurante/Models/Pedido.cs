using System.ComponentModel.DataAnnotations;
using System.ComponetModel.DataAnnottations.Schema;

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
        //Dados de entrega "Congelados" no pedido - não vem do perfil do usuario
        [Required, MaxLength(150)]
        public string Logradourou { get; set; }

        [MaxLength(100)]
        public string? Complemento { get; set; }

        [Required, MaxLength(20)]
        public string Numero { get; set; }

        [Required, MaxLength(100)]
        public string Bairro { get; set; }
        [Required, MaxLength(100)]
        public string Cidade { get; set; }

        [Required, MaxLength(2)]
        public string Estado { get; set; }

        [Required, MaxLength(9)]
        public string Cep { get; set; }
        [Colum(TypeName = "decimal(10,2)")]
        public decimal ValorTotal { get; set; }
        public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();

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
