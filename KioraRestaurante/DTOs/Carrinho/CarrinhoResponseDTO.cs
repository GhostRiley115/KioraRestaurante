using KioraRestaurante.DTOs.ItemCarrinho;

namespace KioraRestaurante.DTOs.Carrinho
{
    //Representa o carrinho inteiro na resposta
    public class CarrinhoResponseDTO
    {
        public int CarrinhoId { get; set; }
        public List<ItemCarrinhoResponseDTO> Itens { get; set; } = new();
        public decimal Total { get; set; }
    }
}
