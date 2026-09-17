using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.DTOs.ItemCarrinho
{
    //Define o que a API precisa devolver do carrinho
    public class ItemCarrinhoResponseDTO
    {
        public int ProdutoId { get; set; }
        public string NomeProduto { get; set; } = string.Empty;
        public string? ImagemUrl { get; set; }
        public decimal PrecoUnitario { get; set; }
        public int Quantidade { get; set; }
        public decimal Subtotal { get; set; }

        // Situação atual do produto no cardápio.
        public bool DisponivelParaCompra { get; set; }
        // Mensagem que a interface pode mostrar ao cliente.
        public string? Aviso { get; set; }
    }
}
