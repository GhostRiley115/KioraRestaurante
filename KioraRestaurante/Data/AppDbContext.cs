using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
        { 
        }

        // Cada DbSet vai virar uma tabela no MySQL, então pra cada classe é preciso adicionar do mesmo jeito do usuario
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Carrinho> Carrinhos { get; set; }
        public DbSet<ItemCarrinho> ItensCarrinho { get; set; }

        //Cria a relação entre tabelas
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // E-MAIL ÚNICO 
            //Impede que dois usuários tenham o mesmo e-mail.

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Usuario 1 : 0..1 Carrinho
            modelBuilder.Entity<Carrinho>()
                .HasOne(c => c.Usuario)
                .WithOne(u => u.Carrinho)
                .HasForeignKey<Carrinho>(c => c.UsuarioId)
                .IsRequired(false);

            // Carrinho 1 : N ItemCarrinho
            modelBuilder.Entity<ItemCarrinho>()
                .HasOne(i => i.Carrinho)
                .WithMany(c => c.Itens)
                .HasForeignKey(i => i.CarrinhoId);

            // Produto 1 : N ItemCarrinho
            modelBuilder.Entity<ItemCarrinho>()
                .HasOne(i => i.Produto)
                .WithMany(p => p.ItensCarrinho)
                .HasForeignKey(i => i.ProdutoId);
        }
    }
}
