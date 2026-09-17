using KioraRestaurante.DTOs.Carrinho;
using KioraRestaurante.DTOs.Pedido;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class CheckoutViewModel
{
    // Campos que o cliente preenche e envia.
    public CriarPedidoRequestDTO Dados { get; set; } = new();

    // Informações que o servidor utiliza para montar a página
    [ValidateNever]
    public CarrinhoResponseDTO Resumo { get; set; } = new();
}