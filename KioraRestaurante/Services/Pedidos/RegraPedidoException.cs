namespace KioraRestaurante.Services.Pedidos;

public class RegraPedidoException : Exception
{
    public RegraPedidoException(string mensagem)
        : base(mensagem)
    {
    }
}