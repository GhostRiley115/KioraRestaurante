using KioraRestaurante.DTOs.Categoria;
using KioraRestaurante.DTOs.Produto;

namespace KioraRestaurante.ViewModels;

public class CardapioViewModel
{
    public List<CategoriaResponseDTO> Categorias { get; set; } = new();

    public List<ProdutoResponseDTO> Produtos { get; set; } = new();

    public int? CategoriaSelecionadaId { get; set; }

    public string Titulo { get; set; } = "Todos os produtos";
}