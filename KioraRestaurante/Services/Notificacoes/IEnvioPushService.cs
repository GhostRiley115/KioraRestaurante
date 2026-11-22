namespace KioraRestaurante.Services.Notificacoes;

// Possíveis resultados de uma tentativa de envio de notificação push.
public enum SituacaoEnvioPush
{
    Desativado = 0,

    AceitoPeloFirebase = 1,

    TokenInvalido = 2,

    FalhaTemporaria = 3,

    FalhaDefinitiva = 4,

    ErroConfiguracao = 5
}

// Resultado individual do envio.
// Não contém credenciais ou tokens FCM.
public sealed record ResultadoEnvioPush(
    SituacaoEnvioPush Situacao,
    string? MensagemId = null,
    string? CodigoErro = null)
{
    public bool Aceito =>
        Situacao ==
            SituacaoEnvioPush.AceitoPeloFirebase;
}

// Contrato de envio de notificações push.
// Permite substituir a integração Firebase por um serviço simulado durante os testes.
public interface IEnvioPushService
{
    bool Habilitado { get; }

    Task<ResultadoEnvioPush> EnviarAsync(
        string tokenFcm,
        Guid notificacaoId,
        CancellationToken ct = default);
}
