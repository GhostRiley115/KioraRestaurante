using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Intarface
{
    public interface ICategoriaService
    {
        Task<List<Categoria>> ListarTodas();
    }
}
