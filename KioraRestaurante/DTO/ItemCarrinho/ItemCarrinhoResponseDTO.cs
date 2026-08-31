namespace KioraRestaurante.DTO.ItemCarrinho
{
    public class ItemCarrinhoResponseDTO
    {
        //Define o que a API precisa devolver do carrinho
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
        public decimal Subtotal { get; set; }
    }
}
