using KioraRestaurante.DTOs.Carrinho;

namespace KioraRestaurante.DTOs.Pedido;

public class CheckoutPedidoResponseDTO
{
    public CarrinhoResponseDTO Carrinho { get; set; } = new();
    public string TokenRevisao { get; set; } = string.Empty;
}