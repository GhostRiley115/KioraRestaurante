using System.ComponentModel.DataAnnotations;
namespace KioraRestaurante.Models;

public enum TipoReserva { Agendada = 0, Fila = 1 }
public enum SituacaoReserva { Confirmada = 0, Aguardando = 1, Chamada = 2, EmAtendimento = 3, Concluida = 4, Cancelada = 5, Expirada = 6 }

public class MesaRestaurante
{
    public int Id { get; set; }
    [MaxLength(30)] public string Nome { get; set; } = "";
    public int Capacidade { get; set; }
    public bool Ativa { get; set; } = true;
}

// Esta linha é bloqueada em transação antes de alterar reservas/mesas.
// Todas as instâncias da aplicação disputam a mesma trava no MySQL.
public class ControleSala { public int Id { get; set; } }

public class Reserva
{
    public int Id { get; set; }
    public Guid Codigo { get; set; } = Guid.NewGuid();
    public Guid ChaveSolicitacao { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    [MaxLength(64)] public string? VisitanteHash { get; set; }
    [MaxLength(100)] public string Nome { get; set; } = "";
    [MaxLength(11)] public string Telefone { get; set; } = "";
    public int Pessoas { get; set; }
    public TipoReserva Tipo { get; set; }
    public SituacaoReserva Situacao { get; set; }
    // Planejamento interno garante espaço; o funcionário confirma/troca a mesa na chegada.
    public int? MesaId { get; set; }
    public MesaRestaurante? Mesa { get; set; }
    public DateTime CriadaEmUtc { get; set; }
    public DateTime? InicioUtc { get; set; }
    public DateTime? FimUtc { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? ChegadaEmUtc { get; set; }
    public DateTime? EncerradaEmUtc { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
    public List<EventoReserva> Eventos { get; set; } = new();
}

public class EventoReserva
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = null!;
    public int? AdministradorId { get; set; }
    public DateTime DataUtc { get; set; }
    [MaxLength(500)] public string Descricao { get; set; } = "";
}
