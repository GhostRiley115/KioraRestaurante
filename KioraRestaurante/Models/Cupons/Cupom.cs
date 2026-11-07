using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models.Cupons;

public enum TipoDescontoCupom { Percentual = 0, ValorFixo = 1 }
public enum SituacaoUsoCupom { Reservado = 0, Confirmado = 1, Cancelado = 2 }

public class Cupom
{
    public int Id { get; set; }
    [MaxLength(40)] public string Codigo { get; set; } = "";
    [MaxLength(200)] public string Descricao { get; set; } = "";
    [MaxLength(100)] public string? Parceiro { get; set; }
    public TipoDescontoCupom Tipo { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Valor { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ValorMinimoProdutos { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? DescontoMaximo { get; set; }
    public DateTime InicioEmUtc { get; set; }
    public DateTime FimEmUtc { get; set; }
    public int? LimiteTotal { get; set; }
    public int LimitePorCliente { get; set; } = 1;
    public int UtilizacoesAtivas { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public int CriadoPorId { get; set; }
    public Usuario CriadoPor { get; set; } = null!;
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
    public List<UsoCupom> Usos { get; set; } = [];
}

// O preço e a identificação do parceiro são os da compra, não os da edição atual do cupom.
public class UsoCupom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CupomId { get; set; }
    public Cupom Cupom { get; set; } = null!;
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    [MaxLength(40)] public string Codigo { get; set; } = "";
    [MaxLength(100)] public string? Parceiro { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Desconto { get; set; }
    public SituacaoUsoCupom Situacao { get; set; }
    public DateTime ReservadoEmUtc { get; set; }
    public DateTime? ConfirmadoEmUtc { get; set; }
    public DateTime? CanceladoEmUtc { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
}
