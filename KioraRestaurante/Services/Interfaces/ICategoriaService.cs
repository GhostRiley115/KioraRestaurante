using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<List<Categoria>> ListarTodas();
    }
}