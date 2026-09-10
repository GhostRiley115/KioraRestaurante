namespace KioraRestaurante.DTOs.ItemCarrinho
{
    public class ItemCarrinhoResponseDTO
    {
        //Define o que a API precisa devolver do carrinho
        public int ProdutoId { get; set; }
        public string NomeProduto { get; set; } = string.Empty;
        public string ImagemUrl { get; set; } = null!;
        public decimal PrecoUnitario { get; set; }
        public int Quantidade { get; set; }
        public decimal Subtotal { get; set; }
    }
}
