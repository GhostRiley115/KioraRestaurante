using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.DTOs.Produto;

namespace KioraRestaurante.ViewModels.Admin;

public class ProdutosAdminViewModel
{
    public FiltroProdutosAdminDTO Filtros { get; set; } = new();

    public ResultadoPaginadoDTO<ProdutoAdminResponseDTO> Resultado
    { get; set; } = new();
}