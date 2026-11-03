namespace KioraRestaurante.ViewModels;

public class FiltroCardapioViewModel
{
    // Seleção automática do catálogo, sem criar uma categoria no banco.
    public string? Colecao { get; set; }
    public string? Busca { get; set; }
    // Textos permitem aceitar tanto 25,90 quanto 25.90 sem depender da cultura da URL.
    public string? PrecoMinimo { get; set; }
    public string? PrecoMaximo { get; set; }
    public string? Ordenacao { get; set; } = "padrao";
    public bool SomenteDisponiveis { get; set; }
    public bool SomentePromocoes { get; set; }
}