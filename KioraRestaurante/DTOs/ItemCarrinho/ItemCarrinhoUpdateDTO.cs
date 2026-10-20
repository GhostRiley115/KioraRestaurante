using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Carrinho;

namespace KioraRestaurante.DTOs.ItemCarrinho
{
    // Define o que é preciso para alterar a quantidade do produto
    public class ItemCarrinhoUpdateDTO : VersaoCarrinhoRequestDTO
    {
        public int Quantidade { get; set; }
    }
}
