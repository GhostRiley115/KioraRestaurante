using KioraRestaurante.DTOs.Categoria;

namespace KioraRestaurante.Services.Produtos
{
    public interface ICategoriaService
    {
        Task<List<CategoriaResponseDTO>> ListarTodas();
    }
}