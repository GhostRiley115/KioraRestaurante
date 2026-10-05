using KioraRestaurante.Models.Enums;


namespace KioraRestaurante.Helpers
{
    public class StatusVisual
    {
        public static string Pedido(StatusPedido status) => status switch
        {
            StatusPedido.Recebido => "status-recebido",
            StatusPedido.EmPreparo => "status-empreparo",
            StatusPedido.SaiuParaEntrega => "status-saiuparaentrega",
            StatusPedido.Entregue => "status-entregue",
            StatusPedido.Cancelado => "status-cancelado",
            _ => ""
        };
        public static string Pagamento(StatusPagamento status) => status switch
        {
            StatusPagamento.Pendente => "status-pendente",
            StatusPagamento.Aprovado => "status-aprovado",
            StatusPagamento.Recusado => "status-recusado",
            StatusPagamento.Cancelado => "status-cancelado",
            _ => ""
        };
    }
}
