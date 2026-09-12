using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.ItemCarrinho
{
    //Define o que é preciso para alterar a quantidade do produto
    public class ItemCarrinhoUpdateDTO
    {
        [Range(1, 99, ErrorMessage = "A quantidade deve estar entre 1 e 99")]
        public int Quantidade { get; set; }
    }
}
