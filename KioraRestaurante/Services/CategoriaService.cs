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
                .OrderBy(c => c.Nome)
                .Select(c => new CategoriaResponseDTO
                {
                    Id = c.Id,
                    Nome = c.Nome,
                })
                .ToListAsync();
        }
    }
}
