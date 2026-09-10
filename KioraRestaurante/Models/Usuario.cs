using KioraRestaurante.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models
{
    public class Usuario
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; }

        [MaxLength(100)]
        public string Nome { get; set; } = null!;
        [MaxLength(150)]
        public string Email { get; set; } = null!;
        public string SenhaHash { get; set; } = null!;
        public bool Ativo { get; set; } = true;

        public TipoUsuario Tipo { get; set; } = TipoUsuario.Cliente;
        [MaxLength(255)]
        public string? TokenRecuperacaoSenha { get; set; }
        public DateTime? ExpiracaoTokenRecuperacaoSenha { get; set; }

        //Permite navegar para o carrinho do usuário em si, Usuario 1 -> carrinho 10, usuario 3 -> carrinho 21. Usuario após o login, tem um carrinho associado a ele.
        public Carrinho? Carrinho { get; set; }

        //Um usuário pode ter vários pedidos. Usuario 1 -> N Pedidos
        public List<Pedido> Pedidos { get; set; } = new();
    }
}
