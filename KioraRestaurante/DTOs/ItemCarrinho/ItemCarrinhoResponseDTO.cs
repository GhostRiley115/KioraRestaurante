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
    }
}
