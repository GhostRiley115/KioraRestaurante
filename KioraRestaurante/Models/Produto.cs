namespace KioraRestaurante.Models
{
    public class Produto
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; }

        //Define que um produto pode estar em vários ItemProduto e cria uma navegação do produto para cada ItemCarrinho que ele esteja.
        public List<ItemCarrinho> ItensCarrinho { get; set; } = new(); 
    }
}
