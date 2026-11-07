using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models.Notificacoes;

public enum PublicoNotificacao { Cliente = 0, Administrador = 1, Todos = 2 }
public enum TipoNotificacao { PedidoRecebido = 0, PedidoEmPreparo = 1, PedidoSaiuParaEntrega = 2, PedidoEntregue = 3,
    PedidoCancelado = 4, PagamentoConfirmado = 5, NovoPedidoAdmin = 6, NovaReservaAdmin = 7, ReservaAtualizada = 8,
    Comunicado = 9, CupomPromocional = 10, ConferirCupomInterno = 11 }

// O evento é salvo junto com o pedido/reserva; a distribuição ocorre depois da gravação confirmada.
public class EventoNotificacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoNotificacao Tipo { get; set; }
    public int? PedidoId { get; set; }
    public Pedido? Pedido { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessadoEmUtc { get; set; }
    public DateTime? ProcessandoAteUtc { get; set; }
    public Guid Concessao { get; set; }
}

public class Notificacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoNotificacao Tipo { get; set; }
    public PublicoNotificacao Publico { get; set; }
    [MaxLength(120)] public string Titulo { get; set; } = "";
    [MaxLength(1500)] public string Mensagem { get; set; } = "";
    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
    public int? AutorId { get; set; }
    public Usuario? Autor { get; set; }
    public int? PedidoId { get; set; }
    public Pedido? Pedido { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public int? CupomId { get; set; }
    public Cupons.Cupom? Cupom { get; set; }
    public List<NotificacaoUsuario> Destinatarios { get; set; } = [];
}

public class NotificacaoUsuario
{
    public Guid NotificacaoId { get; set; }
    public Notificacao Notificacao { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTime? LidaEmUtc { get; set; }
}
