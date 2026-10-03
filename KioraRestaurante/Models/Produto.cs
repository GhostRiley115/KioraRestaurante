using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models
{
    public class Produto
    {
        // O EF Core sabe automaticamente que "Id" é a Chave Primária.
        public int Id { get; set; }

        // Diz para o Ef core o máximo de caracteres possiveis.
        [Required]
        [MaxLength(100)]
        public string Nome { get; set; } = null!;

        [MaxLength(500)]
        public string Descricao { get; set; } = null!;

        // Atribui um preço mínimo e máximo à propriedade.
        [Range(typeof(decimal), "0.01", "99999999", ParseLimitsInInvariantCulture = true)]
        // Cria uma coluna e evita que o mysql mapeie de forma padrão o decimal como numeric
        [Column(TypeName = "decimal(10,2)")]
        public decimal Preco { get; set; }

        [MaxLength(500)]
        public string Imagem { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? ImagemPublicId { get; set; }
        public bool Disponivel { get; set; } = true;
        public bool Ativo { get; set; } = true;
        public int CategoriaId { get; set; }

        public Categoria Categoria { get; set; } = null!;

        /*
         *Define que um produto pode estar em vários ItemProduto e cria
         *uma navegação do produto para cada ItemCarrinho que ele esteja.
         */
        public List<ItemCarrinho> ItensCarrinho { get; set; } = new();

        /*
         *Define que um produto pode estar em vários ItemPedido e cria uma
         *navegação do produto para cada ItemPedido que ele esteja.
         */
        public List<ItemPedido> ItensPedido { get; set; } = new();

        // Identifica se esse produto representa um conjunto de produtos.
        public bool EhCombo { get; set; }

        // Zero significa que não existe desconto aplicado.
        [Range(typeof(decimal), "0", "90",
            ParseLimitsInInvariantCulture = true,
            ErrorMessage = "Informe um desconto entre 0% e 90%.")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DescontoPercentual { get; set; }

        // Componentes que fazem parte deste produto quando ele é um combo.
        public List<ComboComponente> Componentes { get; set; } = new();
    }
}
