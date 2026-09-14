using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Interfaces
{
    public interface IProdutoService
    {
        Task<Produto> Criar(Produto produto);
        Task<List<Produto>> ListarTodos();
        Task<List<Produto>> ListarPorCategoria(int categoriaId);
    }
}