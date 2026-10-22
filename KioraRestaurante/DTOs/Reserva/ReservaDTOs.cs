using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models;
namespace KioraRestaurante.DTOs.Reserva;

public class CriarReservaDTO
{
    public Guid ChaveSolicitacao { get; set; }
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Informe um nome entre 2 e 100 caracteres.")]
    public string Nome { get; set; } = "";
    [Required(ErrorMessage = "Informe o telefone com DDD.")]
    [StringLength(25)] public string Telefone { get; set; } = "";
    [Range(1, 6, ErrorMessage = "Reservas online atendem grupos de 1 a 6 pessoas.")]
    public int Pessoas { get; set; } = 2;
    public DateOnly? Data { get; set; }
    public string? Horario { get; set; }
    public bool AceitaPermanenciaReduzida { get; set; }
}

public record HorarioReservaDTO(string Horario, string Encerramento, int Minutos, bool Disponivel);
public record ReservaResponseDTO(Guid Codigo, string Nome, int Pessoas, TipoReserva Tipo,
    SituacaoReserva Situacao, string SituacaoTexto, DateTime? InicioUtc, DateTime? FimUtc,
    DateTime ExpiraEmUtc, int? PosicaoFila, string? Mesa, bool PodeCancelar, Guid Versao);
public record AcessoReserva(int? UsuarioId, string? VisitanteHash);
public class OperarReservaDTO
{
    public Guid Codigo { get; set; }
    public Guid Versao { get; set; }
    [Required] public string Acao { get; set; } = "";
    public int? MesaId { get; set; }
    [StringLength(400)] public string? Motivo { get; set; }
}
public class CancelarReservaDTO { public Guid Codigo { get; set; } public Guid Versao { get; set; } }
