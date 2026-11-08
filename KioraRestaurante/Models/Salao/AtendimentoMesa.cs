using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace KioraRestaurante.Models.Salao;

public enum SituacaoAtendimento { Aberto = 0, AguardandoLimpeza = 1, Encerrado = 2, Cancelado = 3 }
public enum SituacaoPreparo { NaFila = 0, EmPreparo = 1, Pronto = 2, Entregue = 3, Cancelado = 4 }
public enum MeioPagamentoPresencial { Dinheiro = 0, CartaoCredito = 1, CartaoDebito = 2, Pix = 3, Outro = 4 }
public enum TipoDescontoPresencial { Valor = 0, Percentual = 1 }

// O salão tem sua própria comanda: endereço, frete e cobrança online pertencem somente ao delivery.
public class AtendimentoMesa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)] public string NomeMesa { get; set; } = "";
    public int MesaId { get; set; }
    public MesaRestaurante Mesa { get; set; } = null!;
    // A chave única anulável impede duas comandas ocupando a mesma mesa, inclusive entre instâncias.
    public int? MesaOcupadaId { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public int GarcomId { get; set; }
    public Usuario Garcom { get; set; } = null!;
    [MaxLength(100)] public string NomeGrupo { get; set; } = "";
    public int Pessoas { get; set; }
    public SituacaoAtendimento Situacao { get; set; }
    public DateTime AbertoEmUtc { get; set; }
    public DateTime? FechadoEmUtc { get; set; }
    public DateTime? LiberadoEmUtc { get; set; }
    public int? FechadoPorId { get; set; }
    [MaxLength(100)] public string? GarcomFechamento { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal SubtotalFechamento { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal TotalFechamento { get; set; }
    public TipoDescontoPresencial? TipoDesconto { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal ValorDescontoInformado { get; set; }
    [MaxLength(400)] public string? MotivoDesconto { get; set; }
    public int? DescontoPorId { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
    public List<ItemComanda> Itens { get; set; } = [];
    public List<PagamentoPresencial> Pagamentos { get; set; } = [];
    public List<MovimentoSalao> Historico { get; set; } = [];
}
public class ItemComanda
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AtendimentoId { get; set; }
    public AtendimentoMesa Atendimento { get; set; } = null!;
    public Guid RodadaId { get; set; }
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    [MaxLength(100)] public string NomeProduto { get; set; } = "";
    public int Quantidade { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal PrecoUnitario { get; set; }
    // Histórico da promoção no lançamento; nulo nas comandas anteriores a este registro.
    [Column(TypeName="decimal(10,2)")] public decimal? PrecoOriginalUnitario { get; set; }
    [Column(TypeName="decimal(5,2)")] public decimal? DescontoPercentual { get; set; }
    [MaxLength(500)] public string? Observacao { get; set; }
    [Column(TypeName="text")] public string? Composicao { get; set; }
    [MaxLength(500)] public string? ImagemUrl { get; set; }
    [MaxLength(200)] public string? ImagemPublicId { get; set; }
    public SituacaoPreparo Situacao { get; set; }
    public DateTime EnviadoEmUtc { get; set; }
    public DateTime? IniciadoEmUtc { get; set; }
    public DateTime? ProntoEmUtc { get; set; }
    public DateTime? EntregueEmUtc { get; set; }
    public DateTime? CanceladoEmUtc { get; set; }
    [MaxLength(400)] public string? MotivoCancelamento { get; set; }
    public Guid? SubstituiItemId { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
    [NotMapped] public decimal Subtotal => Quantidade * PrecoUnitario;
}
// Apenas registra o recebimento externo; não processa cartão, Pix ou estorno.
public class PagamentoPresencial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AtendimentoId { get; set; }
    public AtendimentoMesa Atendimento { get; set; } = null!;
    public MeioPagamentoPresencial Meio { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal Valor { get; set; }
    [MaxLength(100)] public string? Referencia { get; set; }
}
// A mesma chave de comando não pode produzir duas alterações após timeout/reenvio do aplicativo.
public class MovimentoSalao
{
    public Guid Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public Guid? AtendimentoId { get; set; }
    public AtendimentoMesa? Atendimento { get; set; }
    public Guid? ResultadoId { get; set; }
    [MaxLength(40)] public string Acao { get; set; } = "";
    [MaxLength(64)] public string DadosHash { get; set; } = "";
    [MaxLength(100)] public string Responsavel { get; set; } = "";
    [MaxLength(1500)] public string Descricao { get; set; } = "";
    public DateTime CriadoEmUtc { get; set; }
}
