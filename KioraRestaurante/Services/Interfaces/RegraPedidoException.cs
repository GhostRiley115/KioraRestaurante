namespace KioraRestaurante.Services.Interfaces;

public class RegraPedidoException : Exception
{
    public RegraPedidoException(string mensagem)
        : base(mensagem)
    {
    }
}