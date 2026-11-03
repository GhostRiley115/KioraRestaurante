namespace KioraRestaurante.Services.Cardapio;

public static class ColecoesCardapio
{
    public const string MaisPedidos = "mais-pedidos";
    public const string Combos = "combos";
    public const string Promocoes = "promocoes";

    public static IReadOnlyDictionary<string, string> Nomes { get; } =
        new Dictionary<string, string>
        {
            [MaisPedidos] = "Mais pedidos",
            [Combos] = "Combos",
            [Promocoes] = "Promoções"
        };

    public static string Normalizar(string? valor) =>
        (valor ?? "").Trim().ToLowerInvariant();
}