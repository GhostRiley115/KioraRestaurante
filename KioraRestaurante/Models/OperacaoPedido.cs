using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KioraRestaurante.Models;

public enum TipoOperacaoPedido { Endereco = 0, Cancelamento = 1 }
public enum EstadoOperacaoPedido { Processando = 0, AguardandoPagamento = 1, AguardandoDevolucao = 2, Concluida = 3, Desfeita = 4, Conferencia = 5 }

// Mantém o pedido atual enquanto a alteração e seus ajustes financeiros são conferidos.
public class OperacaoPedido
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    // A origem administrativa é registrada pelo servidor, sem substituir a identidade do cliente.
    public int? AdministradorSolicitanteId { get; set; }
    [MaxLength(100)] public string? AdministradorSolicitanteNome { get; set; }
    public TipoOperacaoPedido Tipo { get; set; }
    public EstadoOperacaoPedido Estado { get; set; }
    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConcluidaEmUtc { get; set; }
    public DateTime? ProcessandoAteUtc { get; set; }
    public DateTime ConferidaEmUtc { get; set; } = DateTime.UtcNow;
    public bool DesistenciaSolicitada { get; set; }
    public bool PagamentoConfirmado { get; set; }
    [Column(TypeName = "text")] public string EnderecoAnteriorJson { get; set; } = "";
    [Column(TypeName = "text")] public string NovoEnderecoJson { get; set; } = "";
    [Column(TypeName = "text")] public string? EnderecoSalvarJson { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal TaxaAnterior { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal NovaTaxa { get; set; }
    public int DistanciaMetros { get; set; }
    public int DuracaoSegundos { get; set; }
    public int? RevisaoFinanceiraAplicada { get; set; }
    [MaxLength(300)] public string? Motivo { get; set; }
    [MaxLength(400)] public string Mensagem { get; set; } = "";
    // A cobrança anterior só é substituída depois de o provedor confirmar que foi encerrada sem pagamento.
    [Column(TypeName = "text")] public string? PagamentoAnteriorJson { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
    public List<MovimentoPedido> Movimentos { get; set; } = [];
    [NotMapped] public decimal Diferenca => NovaTaxa - TaxaAnterior;
    [NotMapped] public bool Finalizada => Estado is EstadoOperacaoPedido.Concluida or EstadoOperacaoPedido.Desfeita;
}

public enum TipoMovimentoPedido { Cobranca = 0, Devolucao = 1 }
public enum EstadoMovimentoPedido { Pendente = 0, Confirmado = 1, Encerrado = 2, Conferencia = 3 }

// Cada requisição financeira tem corpo e chave próprios e imutáveis para suportar retentativas.
public class MovimentoPedido
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperacaoPedidoId { get; set; }
    public OperacaoPedido Operacao { get; set; } = null!;
    public TipoMovimentoPedido Tipo { get; set; }
    public EstadoMovimentoPedido Estado { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal Valor { get; set; }
    [MaxLength(80)] public string? OrderId { get; set; }
    [MaxLength(80)] public string? TransacaoId { get; set; }
    [MaxLength(80)] public string? ReembolsoId { get; set; }
    [MaxLength(2048)] public string? CheckoutUrl { get; set; }
    [Column(TypeName = "text")] public string Corpo { get; set; } = "";
    [Column(TypeName = "text")] public string ReembolsosAnterioresJson { get; set; } = "[]";
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmadoEmUtc { get; set; }
    [ConcurrencyCheck] public Guid Versao { get; set; } = Guid.NewGuid();
}
