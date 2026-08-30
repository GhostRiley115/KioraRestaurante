//Um Carrinho possui um identificador, pertence a um Usuário e possui uma lista de itens.
namespace KioraRestaurante.Models
{
    public class Carrinho
    {
        public int Id { get; set; }  //EF Core já entende que é uma chave primária apenas pelo "Id". Aqui determinamos o Id de cada carrinho.
        public int UsuarioId { get; set; } //Chave estrangeira do Id de usuário. Relaciona esse carrinho a 1 usuário.
        public Usuario Usuario { get; set; } //Carrinho tem um usuário. Carrinho pertence a um usuário. Pega as informações do usuário que está relacionado a esse carrinho com o Id.
        public List<ItemCarrinho> Itens { get; set; } = new(); //Criando uma lista podendo conter varios ItemCarrinho dentro dela.
    }
}
