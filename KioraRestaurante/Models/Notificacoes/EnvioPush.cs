using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models.Notificacoes;

// Situação de uma notificação na fila de envio push.
public enum SituacaoFilaPush
{
    Pendente = 0,

    AceitoPeloFirebase = 1,

    TokenInvalido = 2,

    FalhaDefinitiva = 3,

    Descartado = 4,

    Expirado = 5
}

// Representa uma tentativa persistente de entregar uma notificação a um dispositivo.

// Não armazena o token FCM nem o conteúdo da mensagem enviada ao usuário.
public sealed class EnvioPush
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    // NOTIFICAÇÃO DE ORIGEM
    public Guid NotificacaoId { get; set; }
    public Notificacao Notificacao { get; set; } =
        null!;

    // DISPOSITIVO DESTINATÁRIO
    public long DispositivoId { get; set; }
    public DispositivoPush Dispositivo { get; set; } =
        null!;

    // IDENTIDADE DO DESTINATÁRIO
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [MaxLength(64)]
    public string? VisitanteHash { get; set; }
    public Guid? VersaoSessaoUsuario { get; set; }

    // Versão do dispositivo no momento em que o envio foi colocado na fila.  
    public Guid VersaoDispositivo { get; set; }

    // CONTROLE DO ENVIO
    public SituacaoFilaPush Situacao { get; set; } =
        SituacaoFilaPush.Pendente;
    public int Tentativas { get; set; }
    public DateTime CriadoEmUtc { get; set; } =
        DateTime.UtcNow;
    public DateTime ProximaTentativaEmUtc { get; set; } =
        DateTime.UtcNow;
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? UltimaTentativaEmUtc { get; set; }
    public DateTime? FinalizadoEmUtc { get; set; }

    // CONTROLE DE PROCESSAMENTO CONCORRENTE
    public DateTime? ProcessandoAteUtc { get; set; }
    public Guid? Concessao { get; set; }

    [ConcurrencyCheck]
    public Guid Versao { get; set; } =
        Guid.NewGuid();

    // RESULTADO DO PROCESSAMENTO

    [MaxLength(100)]
    public string? UltimoCodigoErro { get; set; }

    [MaxLength(500)]
    public string? MensagemFirebaseId { get; set; }
}

