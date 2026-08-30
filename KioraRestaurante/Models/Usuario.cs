using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.Models
{
    public class Usuario
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; } 
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Senha { get; set; }
        public TipoUsuario Tipo { get; set; } = TipoUsuario.Cliente;
        public string? TokenRecuperacaoSenha { get; set; }
        public DateTime? ExpiracaoTokenRecuperacaoSenha { get; set; }

        //Permite navegar para o carrinho do usuário em si, Usuario 1 -> carrinho 10, usuario 3 -> carrinho 21. Usuario após o login, tem um carrinho associado a ele.
        public Carrinho? Carrinho { get; set; } 
    }
}
