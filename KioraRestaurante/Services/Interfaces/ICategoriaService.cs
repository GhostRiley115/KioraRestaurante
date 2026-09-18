using KioraRestaurante.DTOs.Categoria;

namespace KioraRestaurante.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<List<CategoriaResponseDTO>> ListarTodas();
    }
}