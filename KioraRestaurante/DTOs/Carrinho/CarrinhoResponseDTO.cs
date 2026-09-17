using KioraRestaurante.DTOs.ItemCarrinho;

namespace KioraRestaurante.DTOs.Carrinho
{
    //Representa o carrinho inteiro na resposta
    public class CarrinhoResponseDTO
    {
        //Nulo quando ainda não existe um carrinho salvo
        public int? CarrinhoId { get; set; }
        public List<ItemCarrinhoResponseDTO> Itens { get; set; } = new();
        public decimal Total { get; set; }

        //Soma das unidades, utilizada no ícone do carrinho.
        public int QuantidadeTotal { get; set; }
        //Indica se os itens estão aptos para seguir no checkout.
        public bool PodeProsseguir { get; set; }
        //Autenticação necessária para confirmar o pedido.
        public bool RequerLogin { get; set; }
        public bool MesclagemPendente { get; set; }
        public List<string> Avisos { get; set; } = new();
    }
}
