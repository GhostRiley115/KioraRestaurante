namespace KioraRestaurante.DTO.ItemCarrinho
{
    public class ItemCarrinhoCreateDTO
    {
        //Define o que é preciso para adicionar um produto ao carrinho.
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}
