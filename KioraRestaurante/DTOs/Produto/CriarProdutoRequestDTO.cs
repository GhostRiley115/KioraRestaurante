using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Produto;

public class CriarProdutoRequestDTO
{
    [Required(ErrorMessage = "Informe o nome do produto.")]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;
    [Required(ErrorMessage = "Informe a descrição do produto.")]
    [StringLength(500)]
    public string Descricao { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "99999999",
        ErrorMessage = "Informe um preço maior que zero e dentro do limite permitido.")]
    public decimal Preco { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria.")]
    public int CategoriaId { get; set; }
    public bool Disponivel { get; set; }
}