using KioraRestaurante.Models;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Services.Carrinhos;

namespace KioraRestaurante.Services.Pedidos;

public interface IPedidoService
{
    Task<CheckoutPedidoResponseDTO> PrepararCheckout(AcessoCarrinho acesso);

    Task<int> ConfirmarPedido(AcessoCarrinho acesso, CriarPedidoRequestDTO dto);

    Task<Pedido?> BuscarPedido(int usuarioId, int pedidoId);

    Task<List<Pedido>> ListarPedidos(int usuarioId);

}