using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Intarface;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services
{
    public class ProdutoService : IProdutoService
    {
        private readonly AppDbContext _context;

        public ProdutoService(AppDbContext context)
        {
            _context = context;
        }
        //Grava informações que forem criadas
        //o Produtos.Add não salva ainda, só pede pro ef fazer um rascunho dele na memória
        // SaveChangesAsync é oq conversa com o banco e faz o INSERT de fato daquele "rascunho"
        public async Task<Produto> Criar (Produto produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();
            return produto;
        }


        //Só vai listar aquele que tiver o disponivel e ativo como true
        // O Includ é o responsável por trazer as informações da categoria junto
        // Sem ele, a categoria retornaria null 
        public async Task<List<Produto>> ListarTodos()
        {
            return await _context.Produtos
                .Where(p => p.Disponivel && p.Ativo)
                .Include(p => p.Categoria)
                .ToListAsync();
        }

        //Vai filtrar e trazer só a categoria selecionada pelo usuario
        public async Task<List<Produto>> ListarPorCategoria(int categoriaId)
        {
            return await _context.Produtos
                .Where(p => p.CategoriaId == categoriaId && p.Disponivel && p.Ativo)
                .Include(p => p.Categoria)
                .ToListAsync();
        }
    }
}
