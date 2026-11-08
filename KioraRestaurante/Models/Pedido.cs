using System.ComponentModel.DataAnnotations.Schema;
using KioraRestaurante.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;
        public DateTime DataPedido { get; set; } = DateTime.UtcNow;
        
        public EnderecoEntrega EnderecoEntrega { get; set; } = null!;

        public FormaPagamento FormaPagamento { get; set; }
        [ConcurrencyCheck]
        public StatusPedido StatusPedido { get; set; } = StatusPedido.Recebido;
        public StatusPagamento StatusPagamento { get; set; } = StatusPagamento.Pendente;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ValorTotal { get; set; }

        // Valores da entrega no momento da compra; mudanças na tabela não alteram pedidos antigos.
        [Column(TypeName = "decimal(10,2)")]
        public decimal TaxaEntrega { get; set; }
        public int? DistanciaEntregaMetros { get; set; }
        public int? DuracaoPercursoSegundos { get; set; }
        public int? PreparoMinutos { get; set; }

        [NotMapped]
        public int? PrazoTotalMinutos => PreparoMinutos.HasValue && DuracaoPercursoSegundos.HasValue
            ? PreparoMinutos.Value + (int)Math.Ceiling(DuracaoPercursoSegundos.Value / 60m) : null;
        [NotMapped]
        public DateTime? PrevisaoEntregaUtc => PrazoTotalMinutos.HasValue ? DataPedido.AddMinutes(PrazoTotalMinutos.Value) : null;

        [ConcurrencyCheck]
        public Guid Versao { get; set; } = Guid.NewGuid();
        public Guid? OperacaoPendenteId { get; set; }
        public int RevisaoFinanceira { get; set; }
        public DateTime? CanceladoEmUtc { get; set; }
        [MaxLength(300)] public string? MotivoCancelamento { get; set; }
        public bool ReembolsoCancelamentoPendente { get; set; }
        public List<OperacaoPedido> Operacoes { get; set; } = [];
        [MaxLength(40)] public string? CodigoCupom { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal DescontoCupom { get; set; }
        public Cupons.UsoCupom? UsoCupom { get; set; }

        public List<ItemPedido> ItensPedido { get; set; } = new();

        //Se a mesma confirmação for enviada novamente, podemos recuperar o pedido já criado.
        public Guid? ChaveConfirmacao { get; set; }
    }
}
