using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using KioraRestaurante.Options;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services.Notificacoes;

// Serviço de integração do Kiora Restaurante com o Firebase Cloud Messaging.
// Não armazena credenciais no código-fonte e não expõe tokens FCM nos logs.
public sealed class FirebasePushService
    : IEnvioPushService, IDisposable
{
    private readonly FirebaseOptions _options;

    private readonly ILogger<FirebasePushService>
        _logger;

    private readonly object _sincronizacao = new();

    private FirebaseApp? _firebaseApp;

    private bool _descartado;

    public FirebasePushService(
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // SITUAÇÃO DA INTEGRAÇÃO
    public bool Habilitado =>
        _options.Ativado;

    // INICIALIZAÇÃO DO FIREBASE
    private FirebaseMessaging ObterMessaging()
    {
        lock (_sincronizacao)
        {
            ObjectDisposedException.ThrowIf(
                _descartado,
                this);

            if (_firebaseApp != null)
            {
                return FirebaseMessaging
                    .GetMessaging(_firebaseApp);
            }

            if (string.IsNullOrWhiteSpace(
                    _options.ProjetoId))
            {
                throw new InvalidOperationException(
                    "O projeto Firebase não está configurado.");
            }

            // Obtém as credenciais externas por Application Default Credentials.
            // Em produção, serão disponibilizadas pela variável:
            // GOOGLE_APPLICATION_CREDENTIALS
            // Nenhuma chave privada deve ser incluída neste código.

            var credencial =
                GoogleCredential.GetApplicationDefault();

            var configuracao = new AppOptions
            {
                Credential = credencial,

                ProjectId =
                    _options.ProjetoId.Trim()
            };

            // Inicializa uma única instância para este serviço.

            _firebaseApp = FirebaseApp.Create(
                configuracao,
                "KioraPush");

            _logger.LogInformation(
                "Serviço Firebase do Kiora inicializado.");

            return FirebaseMessaging
                .GetMessaging(_firebaseApp);
        }
    }

    // ENVIO INDIVIDUAL
    public async Task<ResultadoEnvioPush> EnviarAsync(
        string tokenFcm,
        Guid notificacaoId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Quando desativado, não inicializa o Firebase nem consulta credenciais.

        if (!Habilitado)
        {
            return new ResultadoEnvioPush(
                SituacaoEnvioPush.Desativado);
        }

        if (string.IsNullOrWhiteSpace(tokenFcm) ||
            tokenFcm.Length > 4096)
        {
            return new ResultadoEnvioPush(
                SituacaoEnvioPush.TokenInvalido,

                CodigoErro: "TOKEN_INVALIDO");
        }

        if (notificacaoId == Guid.Empty)
        {
            return new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaDefinitiva,

                CodigoErro: "NOTIFICACAO_INVALIDA");
        }

        FirebaseMessaging messaging;

        // OBTER CLIENTE AUTENTICADO

        try
        {
            messaging = ObterMessaging();
        }
        catch (Exception erro)
            when (erro is not OperationCanceledException)
        {
            // Registra somente o tipo da exceção.
            // Não registra chaves, tokens ou conteúdos de arquivos secretos.

            _logger.LogError(
                "Falha ao inicializar o Firebase. " +
                "Tipo: {TipoErro}.",
                erro.GetType().Name);

            return new ResultadoEnvioPush(
                SituacaoEnvioPush.ErroConfiguracao,

                CodigoErro: "FIREBASE_CONFIGURACAO");
        }

        // PREPARAR MENSAGEM

        // Utiliza mensagens data-only.
        // O Android receberá um sinal de atualização e consultará os detalhes pela API Kiora.
        // A mensagem não contém informações pessoais, financeiras ou de reservas.

        var mensagem = new Message
        {
            // Compatibilidade com o registro FCM legado.
            // Migrar para FID quando o Mobile estiver preparado.
            #pragma warning disable CS0618
            Token = tokenFcm,
            #pragma warning restore CS0618


            Data = new Dictionary<string, string>
            {
                ["tipo"] = "nova_notificacao",

                ["notificacaoId"] = notificacaoId.ToString("D"),
                                
                ["origem"] = "kiora"
            },

            Android = new AndroidConfig
            {
                Priority = Priority.Normal,

                TimeToLive =
                    TimeSpan.FromHours(24)
            }
        };

        // ENVIAR AO FIREBASE

        try
        {
            var mensagemId =
                await messaging.SendAsync(
                    mensagem,
                    ct);

            _logger.LogDebug(
                "Notificação {NotificacaoId} " +
                "aceita pelo Firebase.",
                notificacaoId);

            return new ResultadoEnvioPush(
                SituacaoEnvioPush.AceitoPeloFirebase,

                MensagemId: mensagemId);
        }
        catch (FirebaseMessagingException erro)
        {
            return TratarErroFirebase(
                notificacaoId,
                erro);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Preserva o comportamento de cancelamento do processamento.

            throw;
        }
        catch (OperationCanceledException)
        {
            return new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,

                CodigoErro: "TEMPO_LIMITE");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning(
                "Falha de comunicação com Firebase. " +
                "Notificação: {NotificacaoId}.",
                notificacaoId);

            return new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,

                CodigoErro: "FALHA_REDE");
        }
        catch (ArgumentException)
        {
            return new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaDefinitiva,

                CodigoErro: "MENSAGEM_INVALIDA");
        }
        catch (Exception erro)
        {
            _logger.LogError(
                "Falha inesperada no envio push. " +
                "Notificação: {NotificacaoId}. " +
                "Tipo: {TipoErro}.",
                notificacaoId,
                erro.GetType().Name);

            return new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,

                CodigoErro: "ERRO_INESPERADO");
        }
    }

    // CLASSIFICAR ERROS DO FIREBASE
    private ResultadoEnvioPush TratarErroFirebase(
        Guid notificacaoId,
        FirebaseMessagingException erro)
    {
        var codigo =
            erro.MessagingErrorCode?.ToString()
            ?? erro.ErrorCode.ToString();

        var situacao =
            erro.MessagingErrorCode switch
            {
                MessagingErrorCode.Unregistered =>
                    SituacaoEnvioPush.TokenInvalido,

                MessagingErrorCode.Unavailable =>
                    SituacaoEnvioPush.FalhaTemporaria,

                MessagingErrorCode.Internal =>
                    SituacaoEnvioPush.FalhaTemporaria,

                MessagingErrorCode.QuotaExceeded =>
                    SituacaoEnvioPush.FalhaTemporaria,

                MessagingErrorCode.InvalidArgument =>
                    SituacaoEnvioPush.FalhaDefinitiva,

                MessagingErrorCode.SenderIdMismatch =>
                    SituacaoEnvioPush.ErroConfiguracao,

                MessagingErrorCode.ThirdPartyAuthError =>
                    SituacaoEnvioPush.ErroConfiguracao,

                _ => erro.ErrorCode switch
                {
                    ErrorCode.Unauthenticated =>
                        SituacaoEnvioPush.ErroConfiguracao,

                    ErrorCode.PermissionDenied =>
                        SituacaoEnvioPush.ErroConfiguracao,

                    ErrorCode.FailedPrecondition =>
                        SituacaoEnvioPush.ErroConfiguracao,

                    ErrorCode.InvalidArgument =>
                        SituacaoEnvioPush.FalhaDefinitiva,

                    ErrorCode.Unavailable =>
                        SituacaoEnvioPush.FalhaTemporaria,

                    ErrorCode.Internal =>
                        SituacaoEnvioPush.FalhaTemporaria,

                    ErrorCode.ResourceExhausted =>
                        SituacaoEnvioPush.FalhaTemporaria,

                    ErrorCode.DeadlineExceeded =>
                        SituacaoEnvioPush.FalhaTemporaria,

                    _ =>
                        SituacaoEnvioPush.FalhaTemporaria
                }
            };

        _logger.LogWarning(
            "Envio push não concluído. " +
            "Notificação: {NotificacaoId}. " +
            "Código: {Codigo}. " +
            "Situação: {Situacao}.",

            notificacaoId,
            codigo,
            situacao);

        return new ResultadoEnvioPush(
            situacao,

            CodigoErro: codigo);
    }

    // LIBERAR RECURSOS
    public void Dispose()
    {
        lock (_sincronizacao)
        {
            if (_descartado)
            {
                return;
            }

            _firebaseApp?.Delete();

            _firebaseApp = null;

            _descartado = true;
        }
    }
}

