namespace KioraRestaurante.ViewModels;

public class FiltroCardapioViewModel
{
    public string? Busca { get; set; }
    // Textos permitem aceitar tanto 25,90 quanto 25.90 sem depender da cultura da URL.
    public string? PrecoMinimo { get; set; }
    public string? PrecoMaximo { get; set; }
    public string? Ordenacao { get; set; } = "padrao";
    public bool SomenteDisponiveis { get; set; }
    public bool SomentePromocoes { get; set; }
}