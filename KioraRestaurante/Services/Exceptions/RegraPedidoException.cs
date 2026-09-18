namespace KioraRestaurante.Services.Exceptions;

public class RegraPedidoException : Exception
{
    public RegraPedidoException(string mensagem)
        : base(mensagem)
    {
    }
}