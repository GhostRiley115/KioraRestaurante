using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models
{
    public class EnderecoEntrega
    {
        public int Id { get; set; }

        public int PedidoId { get; set; }
        public Pedido Pedido { get; set; } = null!;

        [Required]
        [MaxLength(9)]
        public string Cep { get; set; } = null!;
        [Required]
        [MaxLength(150)]
        public string Logradouro { get; set; } = null!;
        [Required]
        [MaxLength(20)]
        public string Numero { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Bairro { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Cidade { get; set; } = null!;
        [Required]
        [MaxLength(2)]
        public string Uf { get; set; } = null!;
        [MaxLength(200)]
        public string? Complemento { get; set; }
        [MaxLength(200)]
        public string? Referencia { get; set; }
    }
}
