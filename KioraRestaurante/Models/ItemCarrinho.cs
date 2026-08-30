//Essa classe cria um Objeto com UM produto e sua Quantidade e relaciona a um Carrinho. 

namespace KioraRestaurante.Models
{
    public class ItemCarrinho
    {
        //EF Core já entende que é uma chave primária apenas pelo "Id".
        public int Id { get; set; }

        //Chave estrangeira do Id de carrinho. Relaciona essa classe a 1 carrinho.
        public int CarrinhoId { get; set; }
        //ItemCarrinho tem um carrinho. ItemCarrinho pertence a um carrinho. Relaciona esse item a 1 carrinho utilizando o Id.
        public Carrinho Carrinho { get; set; } = null!;

        //Chave estrangeira do Id de produto. Relaciona 1 produto a essa classe.
        public int ProdutoId { get; set; }
        //ItemCarrinho tem um Produto. Produto pode pertencer a varios ItemProduto. Pega as informações do produto que está relacionado a esse ItemCarrinho com o Id.
        public Produto Produto { get; set; } = null!;

        //Quantidade de vezes que o produto foi adicionado.
        public int Quantidade { get; set; } 
    }
}
