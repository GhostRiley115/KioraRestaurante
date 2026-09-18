using KioraRestaurante.DTOs.Produto;
using Microsoft.AspNetCore.Http;

namespace KioraRestaurante.Services.Interfaces
{
    public interface IProdutoService
    {
        Task<int> Criar(int administradorId, CriarProdutoRequestDTO dto, IFormFile foto);
        Task<List<ProdutoResponseDTO>> ListarCardapio(int? categoriaId = null);
    }
}