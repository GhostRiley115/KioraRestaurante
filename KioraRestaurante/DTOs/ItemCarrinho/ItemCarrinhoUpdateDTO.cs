using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.ItemCarrinho
{
    //Define o que é preciso para alterar a quantidade do produto
    public class ItemCarrinhoUpdateDTO
    {
        public int Quantidade { get; set; }
    }
}
