using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Intarface
{
    public interface IProdutoService
    {
        Task<Produto> Criar(Produto produto);
        Task<List<Produto>> ListarTodos();
        Task<List<Produto>> ListarPorCategoria(int categoriaId);
    }
}
