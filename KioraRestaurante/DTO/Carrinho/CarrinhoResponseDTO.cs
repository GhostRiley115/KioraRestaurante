using KioraRestaurante.DTO.ItemCarrinho;

namespace KioraRestaurante.DTO.Carrinho
{
    public class CarrinhoResponseDTO
    {
        //Representa o carrinho inteiro na resposta
        public List<ItemCarrinhoResponseDTO> Itens { get; set; } = new();
        public decimal Total { get; set; }
    }
}
