namespace KioraRestaurante.Services.Exceptions;

public class RegraProdutoException : Exception
{
    public RegraProdutoException(string mensagem)
        : base(mensagem)
    {
    }
}