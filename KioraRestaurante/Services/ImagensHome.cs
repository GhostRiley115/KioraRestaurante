namespace KioraRestaurante.Services;

// Apenas URLs públicas de imagens estáticas. Nenhuma credencial é enviada ao navegador.
// Os originais locais ficam preservados; o Cloudinary entrega versões menores sob demanda.
public static class ImagensHome
{
    private static readonly IReadOnlyDictionary<string, string> Imagens = new Dictionary<string, string>
    {
        ["fundo-kiora1"] = "https://res.cloudinary.com/oaudlsoj/image/upload/v1792565786/kiora/home/fundo-kiora1-2fd33be9d802.png",
        ["cardapio-card"] = "https://res.cloudinary.com/oaudlsoj/image/upload/v1792565788/kiora/home/cardapio-card-d5f8ef793804.png",
        ["mais-vendido"] = "https://res.cloudinary.com/oaudlsoj/image/upload/v1792565790/kiora/home/mais-vendido-6924f330042b.png",
        ["promocao"] = "https://res.cloudinary.com/oaudlsoj/image/upload/v1792565792/kiora/home/promocao-09d547cdbdad.png",
        ["produtos"] = "https://res.cloudinary.com/oaudlsoj/image/upload/v1792565794/kiora/home/produtos-e498ad2fccb5.png",
    };

    public static string Url(string nome, int largura)
    {
        if (!Imagens.TryGetValue(nome, out var original)) throw new ArgumentException("Imagem da home não cadastrada.", nameof(nome));
        return original.Replace("/image/upload/", $"/image/upload/c_limit,w_{largura}/f_auto/q_auto/");
    }
}
