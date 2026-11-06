using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace KioraRestaurante.Models;
public class PagamentoOnline
{
    [Key, ForeignKey(nameof(Pedido))] public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public Guid Referencia { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public string? OrderId { get; set; }
    [MaxLength(2048)] public string? CheckoutUrl { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Valor { get; set; }
    // Persiste o mesmo corpo para repetir uma criação após timeout com a mesma chave.
    [Column(TypeName = "text")] public string CorpoCriacao { get; set; } = "";
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizacaoExternaUtc { get; set; }
    public DateTime ConferidoEmUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(150)] public string SituacaoExterna { get; set; } = "";
    public bool RevisaoNecessaria { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
}