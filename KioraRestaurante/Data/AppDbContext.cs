using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Cada DbSet vai virar uma tabela no MySQL, então pra cada classe é preciso adicionar do mesmo jeito do usuario
        public DbSet<Usuario> Usuarios { get; set; }
    }
}
