namespace KioraRestaurante.Services.Imagens;

public static class IdentificadorImagemProduto
{
    // Aceita somente identificadores no formato gerado por Enviar.
    public static bool Gerenciado(string? publicId)
    {
        const string prefixo = "kiora/produtos/";
        return publicId != null
               && publicId.StartsWith(prefixo, StringComparison.Ordinal)
               && Guid.TryParseExact(publicId[prefixo.Length..], "N", out _);
    }
}
