using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.Models
{
    public class Usuario
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; }
        public string Nome { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Senha { get; set; } = null!;

        public TipoUsuario Tipo { get; set; } = TipoUsuario.Cliente;
        public string? TokenRecuperacaoSenha { get; set; }
        public DateTime? ExpiracaoTokenRecuperacaoSenha { get; set; }

        //Permite navegar para o carrinho do usuário em si, Usuario 1 -> carrinho 10, usuario 3 -> carrinho 21. Usuario após o login, tem um carrinho associado a ele.
        public Carrinho? Carrinho { get; set; }

        //Um usuário pode ter vários pedidos. Usuario 1 -> N Pedidos
        public List<Pedido> Pedidos { get; set; } = new();
    }
}
