//Um Carrinho possui um identificador, pertence a um Usuário e possui uma lista de itens.

using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models
{
    public class Carrinho
    {
        /*EF Core já entende que é uma chave primária apenas pelo "Id". Aqui determinamos
        o Id de cada carrinho.*/
        public int Id { get; set; }

        /*Chave estrangeira do Id de usuário. Relaciona esse carrinho a 1 usuário. Define também
        que o carrinho pode ou não estar associado a um cliente. Um visitante pode ter um carrinho
        mesmo sem estar logado*/
        public int? UsuarioId { get; set; }

        /*Carrinho pode ter um usuário. Carrinho pode pertencer a um usuário. Pega as informações
        do usuário que está relacionado a esse carrinho com o Id.*/
        public Usuario? Usuario { get; set; }

        //Criando uma lista podendo conter varios ItemCarrinho dentro dela.
        public List<ItemCarrinho> ItensCarrinho { get; set; } = new();

        /*Somente carrinhos de visitante possuem expiração. Quando o
         carrinho passa a pertencer ao usuário, está propriedade fica nula.*/
        public DateTime? ExpiraEmUtc { get; set; }

        /*Permite detectar se outra requisição alterou
         o mesmo carrinho antes de salvarmos nossa alteração.*/
        [ConcurrencyCheck]
        public Guid Versao { get; set; } = Guid.NewGuid();
    }
}
