using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto;

public class FiltroProdutosAdminDTO
{
    [StringLength(100)]
    public string? Busca { get; set; }

    // null significa "todos", sem aplicar esse filtro.
    public bool? Ativo { get; set; }

    public bool? Disponivel { get; set; }

    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;
}