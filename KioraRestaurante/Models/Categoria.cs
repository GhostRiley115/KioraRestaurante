using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models
{
    public class Categoria
    {
        //O EF Core identifica automaticamente "Id" como Chave Primária.
        public int Id { get; set; }

        //Define que o nome da categoria é obrigatório.
        [Required]
        //Define o tamanho máximo do nome da categoria.
        [MaxLength(50)]
        public string Nome { get; set; } = null!;

        //Define o relacionamento entre Categoria e Produto.
        //Uma categoria pode possuir vários produtos.
        public List<Produto> Produtos { get; set; } = new();
    }
}
