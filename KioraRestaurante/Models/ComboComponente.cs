using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models;

public class ComboComponente
{
    public int Id { get; set; }

    // Produto que representa o combo.
    public int ComboId { get; set; }
    public Produto Combo { get; set; } = null!;

    // Produto comum que faz parte do combo.
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;

    [Range(1, 30)]
    public int Quantidade { get; set; }
}