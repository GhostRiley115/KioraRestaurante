using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Intarface;
using KioraRestaurante.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services
{
    public class CategoriaService : ICategoriaService
    {
        // injeção de dependencia sem utilizar o newAppdbContext
        // o proprio asp.net entrega automaticamente
        // a variavel so recebe o valor uma vez no construtor, sendo uma trava de segurança para bugs
        private readonly AppDbContext _context;

        public CategoriaService(AppDbContext context)
        {
            _context = context;
        }


        //parte onde aponta para a tabela categopria
        // filtra as categorias ativas
        //monta um gatilho com o toListAsync
        // só envia os dados quando a resposta do bamnco voltar
        // semtravar a compilação
        public async Task<List<Categoria>> ListarTodas()
        {
            return await _context.Categorias
                .Where(c => c.Ativa)
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }
    }
}
