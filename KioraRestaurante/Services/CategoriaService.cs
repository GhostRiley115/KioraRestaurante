using KioraRestaurante.DTOs.Categoria;
using KioraRestaurante.Data;
using KioraRestaurante.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services
{
    public class CategoriaService : ICategoriaService
    {
        private readonly AppDbContext _context;

        public CategoriaService(AppDbContext context)
        {
            _context = context;
        }

        // parte onde aponta para a tabela categopria
        // filtra as categorias ativas
        public async Task<List<CategoriaResponseDTO>> ListarTodas()
        {
            return await _context.Categorias
                .AsNoTracking()
                .Where(c => c.Ativa)
                // Ordem de apresentação do cardápio; categorias novas aparecem depois.
                // Não alteramos os IDs: os produtos mantêm seus relacionamentos.
                .OrderBy(c => c.Nome == "Entradas" ? 0
                    : c.Nome == "Pratos Principais" ? 1
                    : c.Nome == "Sobremesas" ? 2
                    : c.Nome == "Bebidas" ? 3 : 4)
                .ThenBy(c => c.Nome)
                .Select(c => new CategoriaResponseDTO
                {
                    Id = c.Id,
                    Nome = c.Nome,
                })
                .ToListAsync();
        }
    }
}
