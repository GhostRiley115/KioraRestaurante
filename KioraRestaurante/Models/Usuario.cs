namespace KioraRestaurante.Models
{
    public class Usuario
    {
        public int Id { get; set; } // O EF Core sabe automaticamente que "Id" é a Chave Primária
        public string Nome { get; set; }
        public string Email { get; set; }
    }
}
