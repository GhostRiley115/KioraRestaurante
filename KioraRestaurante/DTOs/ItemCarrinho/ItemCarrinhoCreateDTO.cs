using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.ItemCarrinho
{
    //Define o que é preciso para adicionar um produto ao carrinho.
    public class ItemCarrinhoCreateDTO
    {
        [Range(1, int.MaxValue, ErrorMessage = "Informe um produto válido")]
        public int ProdutoId { get; set; }
        [Range(1, 99, ErrorMessage = "A quantidade deve estar entre 1 e 99")]
        public int Quantidade { get; set; }
    }
}
