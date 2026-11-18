namespace KioraRestaurante.Services.Produtos;

public class RegraProdutoException : Exception
{
    public RegraProdutoException(string mensagem)
        : base(mensagem)
    {
    }
}