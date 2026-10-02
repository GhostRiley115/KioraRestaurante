using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.ViewModels.Admin;

// Traduz os nomes técnicos dos enums somente para apresentação.
public static class TextosAdmin
{
    public static string Pedido(StatusPedido status) => status switch {
        StatusPedido.EmPreparo => "Em preparo", StatusPedido.SaiuParaEntrega => "Saiu para entrega", _ => status.ToString()
    };
    public static string Pagamento(FormaPagamento forma) => forma switch {
        FormaPagamento.CartaoCredito => "Cartão de crédito", FormaPagamento.CartaoDebito => "Cartão de débito", _ => forma.ToString()
    };
}
