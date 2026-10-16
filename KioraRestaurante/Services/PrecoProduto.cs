using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Services;

// Regra única para calcular o valor de venda.
public static class PrecoProduto
{
    public static decimal Calcular(decimal precoOriginal, decimal descontoPercentual)
    {
        if (precoOriginal <= 0)
        {
            throw new ValidationException(
                "O preço do produto deve ser maior que zero.");
        }

        if (descontoPercentual < 0 || descontoPercentual > 90)
        {
            throw new ValidationException(
                "O desconto deve estar entre 0% e 90%.");
        }

        if (decimal.Round(descontoPercentual, 2) != descontoPercentual)
        {
            throw new ValidationException(
                "O desconto deve ter no máximo duas casas decimais.");
        }

        var valor = precoOriginal * (1 - descontoPercentual / 100m);

        // Arredonda o preço de uma unidade antes de multiplicar a quantidade.
        var resultado = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

        if (resultado < 0.01m)
        {
            throw new ValidationException(
                "O preço final deve ser de pelo menos R$ 0,01.");
        }

        return resultado;
    }
}