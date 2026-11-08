using KioraRestaurante.DTOs.Produto;

namespace KioraRestaurante.ViewModels;

public class SecaoCardapioViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public List<ProdutoResponseDTO> Produtos { get; set; } = new();
}