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
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Carrinho> Carrinhos { get; set; }
        public DbSet<ItemCarrinho> ItensCarrinho { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<ItemPedido> ItensPedido { get; set; }
        public DbSet<EnderecoEntrega> EnderecoEntregas { get; set; } 

        //Cria a relação entre tabelas
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //E-MAIL ÚNICO 
            //Impede que dois usuários tenham o mesmo e-mail.
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            //Define Disponivel em Produto -> true como default
            modelBuilder.Entity<Produto>()
                .Property(p => p.Disponivel)
                .HasDefaultValue(true);
            //Define Ativo em Produto -> true como default
            modelBuilder.Entity<Produto>()
                .Property(p => p.Ativo)
                .HasDefaultValue(true);
            //Define Ativo em Usuario -> true como default
            modelBuilder.Entity<Usuario>()
                .Property(u => u.Ativo)
                .HasDefaultValue(true);
            //Define Ativo em Categoria -> true como default
            modelBuilder.Entity<Categoria>()
                .Property(c => c.Ativa)
                .HasDefaultValue(true);

            //Categoria 1 : N Produto
            //Define que uma categoria pode possuir vários produtos.
            modelBuilder.Entity<Produto>()
                .HasOne(p => p.Categoria)
                .WithMany(c => c.Produtos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            //Usuario 1 : 0..1 Carrinho
            modelBuilder.Entity<Carrinho>()
                .HasOne(c => c.Usuario)
                .WithOne(u => u.Carrinho)
                .HasForeignKey<Carrinho>(c => c.UsuarioId)
                .IsRequired(false);

            //Usuario 1 : N Pedido
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Usuario)
                .WithMany(u => u.Pedidos)
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict); //impede o histórico de sumir caso um usuario seja apagado.

            //Carrinho 1 : N ItemCarrinho
            modelBuilder.Entity<ItemCarrinho>()
                .HasOne(i => i.Carrinho)
                .WithMany(c => c.ItensCarrinho)
                .HasForeignKey(i => i.CarrinhoId);

            //Produto 1 : N ItemCarrinho
            modelBuilder.Entity<ItemCarrinho>()
                .HasOne(i => i.Produto)
                .WithMany(p => p.ItensCarrinho)
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            //Produto 1 : N ItemPedido
            modelBuilder.Entity<ItemPedido>()
                .HasOne(i => i.Produto)
                .WithMany(p => p.ItensPedido)
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict); //impede o histórico de sumir caso um produto seja apagado.

            //Pedido 1 : 1 EnderecoEntrega
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.EnderecoEntrega)
                .WithOne(e => e.Pedido)
                .HasForeignKey<EnderecoEntrega>(e => e.PedidoId);

            //Pedido 1 : N ItemPedido
            modelBuilder.Entity<ItemPedido>()
                .HasOne(i => i.Pedido)
                .WithMany(p => p.ItensPedido)
                .HasForeignKey(i => i.PedidoId);

            //Configurando para o banco mostrar o valor dos enum em string e não só o número.
            modelBuilder.Entity<Pedido>()
                .Property(p => p.StatusPagamento)
                .HasConversion<string>()
                .HasMaxLength(30);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.StatusPedido)
                .HasConversion<string>()
                .HasMaxLength(30);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.FormaPagamento)
                .HasConversion<string>()
                .HasMaxLength(30);

            modelBuilder.Entity<Usuario>()
                .Property(u => u.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20);

            //Dentro do mesmo carrinho, um determinado produto só pode aparecer uma vez.
            modelBuilder.Entity<ItemCarrinho>()
                .HasIndex(i => new { i.CarrinhoId, i.ProdutoId })
                .IsUnique();

            //SEED - Categorias iniciais do cardápio
            modelBuilder.Entity<Categoria>().HasData(
                new Categoria { Id = 1, Nome = "Entradas", Ativa = true},
                new Categoria { Id = 2, Nome ="Pratos Principais", Ativa = true},
                new Categoria { Id = 3, Nome = "Bebidas", Ativa = true},
                new Categoria { Id = 4, Nome = "Sobremesas", Ativa = true}
                );
        }
    }
}
