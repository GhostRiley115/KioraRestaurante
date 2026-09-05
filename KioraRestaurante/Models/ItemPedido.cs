using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models
{
    public class ItemPedido
    {
        public int Id { get; set; }

        public int PedidoId { get; set; }
        public Pedido Pedido { get; set; } = null!;
        public int ProdutoId { get; set; }
        public Produto Produto { get; set; } = null!;
        public int Quantidade { get; set; }

        //snapshot do preço no momento da compra
        //evita com que quando o proprietario mude o preço 
        //mude o valor de um pedido no dia anterior
        [Column(TypeName = "decimal(10,2)")]
        public decimal PrecoUnitario { get; set; }

        //Esse atributo fala para pro EF Core não crie
        //uma coluna disso no banco, sendo muito util para 
        //usar direto na view sem repetir a conta lá
        [NotMapped]
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
