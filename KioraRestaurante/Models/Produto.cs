using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models
{
    public class Produto
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; }
        //Diz para o Ef core o máximo de caracteres possiveis
        [Required]
        [MaxLegth(100)]
        public string? Nome { get; set; }

        [MaxLength(500)]
        public string? Descricao { get; set; }

        // Cria uma coluna e evita que o provider do postgres mapeie o decimal como numeric
        [Column(TypeName = "decimal(10,2)")]
        public decimal Preco { get; set; }
        public string? Imagem { get; set; }
        public bool Disponivel { get; set; } = true;
        public int CategoriaId { get; set; }
        public Categoria Categoria { get; set; }

        //Define que um produto pode estar em vários ItemProduto e cria uma navegação do produto para cada ItemCarrinho que ele esteja.
        public List<ItemCarrinho> ItensCarrinho { get; set; } = new();

    }
}
