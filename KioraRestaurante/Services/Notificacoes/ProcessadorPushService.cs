using System.Security.Cryptography;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Notificacoes;

// Processa os registros persistentes da fila de notificações push do Kiora.
// Valida novamente o destinatário antes de encaminhar a mensagem ao Firebase.
public sealed class ProcessadorPushService(
    AppDbContext db,
    IEnvioPushService firebase,
    IDataProtectionProvider protecao,
    TimeProvider relogio,
    ILogger<ProcessadorPushService> logger)
{
    private const int LimiteLote = 30;

    private const int MaximoTentativas = 6;

    private readonly IDataProtector _protetor =
        protecao.CreateProtector(
            "Kiora.Push.FcmToken.v1");

    private static readonly TimeSpan
        DuracaoConcessao =
            TimeSpan.FromMinutes(3);

    private static readonly TimeSpan
        TempoLimiteEnvio =
            TimeSpan.FromSeconds(30);

    // PROCESSAR LOTE DE NOTIFICAÇÕES
    public async Task<int> Processar(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!firebase.Habilitado)
        {
            return 0;
        }

        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var ids = await db.EnviosPush
            .AsNoTracking()
            .Where(e =>
                e.Situacao ==
                    SituacaoFilaPush.Pendente &&

                (
                    e.ProximaTentativaEmUtc <= agora ||
                    e.ExpiraEmUtc <= agora
                ) &&

                (
                    e.ProcessandoAteUtc == null ||
                    e.ProcessandoAteUtc <= agora
                ))
            .OrderBy(e => e.ProximaTentativaEmUtc)
            .ThenBy(e => e.Id)
            .Select(e => e.Id)
            .Take(LimiteLote)
            .ToListAsync(ct);

        var processados = 0;

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (await ProcessarIndividual(
                        id,
                        ct))
                {
                    processados++;
                }
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                // A concessão expira automaticamente.
                // O próximo processamento poderá recuperar o registro.

                logger.LogError(
                    "Falha no processamento push. " +
                    "Envio: {EnvioId}. " +
                    "Tipo: {TipoErro}.",
                    id,
                    erro.GetType().Name);
            }
        }

        return processados;
    }

    // PROCESSAR ENVIO INDIVIDUAL
    private async Task<bool> ProcessarIndividual(
        Guid id,
        CancellationToken ct)
    {
        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var concessao = Guid.NewGuid();

        var versao = Guid.NewGuid();

        // Obtém a concessão de maneira atômica.
        // Somente um processador poderá assumir o registro enquanto ela estiver válida.

        var adquiridos = await db.EnviosPush
            .Where(e =>
                e.Id == id &&

                e.Situacao ==
                    SituacaoFilaPush.Pendente &&

                (
                    e.ProximaTentativaEmUtc <= agora ||
                    e.ExpiraEmUtc <= agora
                ) &&

                (
                    e.ProcessandoAteUtc == null ||
                    e.ProcessandoAteUtc <= agora
                ))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(
                        e => e.Concessao,
                        (Guid?)concessao)
                    .SetProperty(
                        e => e.ProcessandoAteUtc,
                        (DateTime?)agora.Add(
                            DuracaoConcessao))
                    .SetProperty(
                        e => e.Versao,
                        versao),
                ct);

        if (adquiridos != 1)
        {
            return false;
        }

        var envio = await db.EnviosPush
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Id == id &&
                    e.Concessao == concessao,
                ct);

        if (envio == null)
        {
            return false;
        }

        // VERIFICAR EXPIRAÇÃO

        if (envio.ExpiraEmUtc <=
            relogio.GetUtcNow().UtcDateTime)
        {
            await PersistirResultado(
                envio,
                concessao,
                SituacaoFilaPush.Expirado,
                envio.Tentativas,
                "ENVIO_EXPIRADO",
                ct: ct);

            return true;
        }

        if (envio.Tentativas >= MaximoTentativas)
        {
            await PersistirResultado(
                envio,
                concessao,
                SituacaoFilaPush.FalhaDefinitiva,
                envio.Tentativas,
                "LIMITE_TENTATIVAS",
                ct: ct);

            return true;
        }

        // VALIDAR DESTINATÁRIO E DISPOSITIVO

        var dispositivo =
            await ObterDispositivoAutorizado(
                envio,
                ct);

        if (dispositivo == null)
        {
            await PersistirResultado(
                envio,
                concessao,
                SituacaoFilaPush.Descartado,
                envio.Tentativas,
                "DESTINATARIO_NAO_AUTORIZADO",
                ct: ct);

            return true;
        }

        // RECUPERAR TOKEN PROTEGIDO

        string tokenFcm;

        try
        {
            tokenFcm = _protetor.Unprotect(
                dispositivo.TokenFcm);
        }
        catch (CryptographicException)
        {
            // Uma falha de descriptografia não significa que o token FCM é inválido.
            // Pode indicar perda ou indisponibilidade das chaves de Data Protection.

            logger.LogError(
                "Não foi possível recuperar o " +
                "token protegido do dispositivo " +
                "{DispositivoId}.",
                dispositivo.Id);

            await PersistirResultado(
                envio,
                concessao,
                SituacaoFilaPush.Pendente,
                envio.Tentativas,
                "TOKEN_PROTECAO_INDISPONIVEL",
                proximaTentativa:
                    relogio.GetUtcNow()
                        .UtcDateTime.AddHours(1),
                ct: ct);

            return true;
        }

        // ENVIAR AO FIREBASE

        // Confirma que ainda possui a concessão antes de iniciar a comunicação externa.

        agora =
            relogio.GetUtcNow().UtcDateTime;

        var concessaoValida =
            await db.EnviosPush
                .AsNoTracking()
                .AnyAsync(
                    e => e.Id == envio.Id &&
                        e.Concessao == concessao &&
                        e.Versao == envio.Versao &&
                        e.ProcessandoAteUtc >
                            agora.Add(TempoLimiteEnvio) &&
                        e.ExpiraEmUtc > agora &&
                        e.Situacao ==
                            SituacaoFilaPush.Pendente,
                    ct);

        if (!concessaoValida)
        {
            return false;
        }

        ResultadoEnvioPush resultado;

        using var limiteTempo =
            CancellationTokenSource
                .CreateLinkedTokenSource(ct);

        limiteTempo.CancelAfter(
            TempoLimiteEnvio);

        try
        {
            resultado =
                await firebase.EnviarAsync(
                    tokenFcm,
                    envio.NotificacaoId,
                    limiteTempo.Token);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            resultado = new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,

                CodigoErro: "TEMPO_LIMITE");
        }

        // TRATAR RESULTADO

        await TratarResultado(
            envio,
            concessao,
            resultado,
            ct);

        return true;
    }

    // VALIDAÇÃO DE SEGURANÇA

    private async Task<DispositivoPush?>
        ObterDispositivoAutorizado(
            EnvioPush envio,
            CancellationToken ct)
    {
        // Nesta versão, somente destinatários autenticados são processados.
        // O suporte aos visitantes será incluído na integração específica de reservas.

        if (!envio.UsuarioId.HasValue ||
            !envio.VersaoSessaoUsuario.HasValue ||
            envio.VersaoSessaoUsuario == Guid.Empty ||
            envio.VersaoDispositivo == Guid.Empty)
        {
            return null;
        }

        var usuarioId =
            envio.UsuarioId.Value;

        var versaoSessao =
            envio.VersaoSessaoUsuario.Value;

        // Confirma a associação original do dispositivo e a validade da sessão.

        var dispositivo =
            await db.DispositivosPush
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    d =>
                        d.Id ==
                            envio.DispositivoId &&

                        d.Ativo &&

                        d.UsuarioId ==
                            usuarioId &&

                        d.VisitanteHash ==
                            envio.VisitanteHash &&

                        d.Versao ==
                            envio.VersaoDispositivo &&

                        d.VersaoSessaoUsuario ==
                            versaoSessao &&

                        d.Usuario != null &&

                        d.Usuario.Ativo &&

                        !d.Usuario.ConvitePendente &&

                        d.Usuario.VersaoSessao ==
                            versaoSessao,
                    ct);

        if (dispositivo == null)
        {
            return null;
        }

        // Verifica novamente se o usuário continua sendo destinatário autorizado da notificação original.

        var autorizado =
            await db.NotificacoesUsuarios
                .AsNoTracking()
                .AnyAsync(
                    n =>
                        n.NotificacaoId ==
                            envio.NotificacaoId &&

                        n.UsuarioId ==
                            usuarioId &&

                        n.Usuario.Ativo &&

                        !n.Usuario.ConvitePendente &&

                        n.Usuario.VersaoSessao ==
                            versaoSessao &&

                        (
                            n.Notificacao.Publico ==
                                PublicoNotificacao.Todos ||

                            (
                                n.Notificacao.Publico ==
                                    PublicoNotificacao.Cliente &&

                                n.Usuario.Tipo ==
                                    TipoUsuario.Cliente
                            ) ||

                            (
                                n.Notificacao.Publico ==
                                    PublicoNotificacao.Administrador &&

                                (
                                    n.Usuario.Tipo ==
                                        TipoUsuario.Administrador ||

                                    n.Usuario.Tipo ==
                                        TipoUsuario.Gerente
                                )
                            )
                        ),
                    ct);

        return autorizado
            ? dispositivo
            : null;
    }

    // CLASSIFICAR RESULTADO DO ENVIO
    private async Task TratarResultado(
        EnvioPush envio,
        Guid concessao,
        ResultadoEnvioPush resultado,
        CancellationToken ct)
    {
        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var tentativas =
            envio.Tentativas + 1;

        switch (resultado.Situacao)
        {
            case SituacaoEnvioPush.AceitoPeloFirebase:

                await PersistirResultado(
                    envio,
                    concessao,
                    SituacaoFilaPush.AceitoPeloFirebase,
                    tentativas,
                    mensagemFirebaseId:
                        resultado.MensagemId,
                    registrarTentativa: true,
                    ct: ct);

                break;

            case SituacaoEnvioPush.TokenInvalido:

                await PersistirResultado(
                    envio,
                    concessao,
                    SituacaoFilaPush.TokenInvalido,
                    tentativas,
                    resultado.CodigoErro,
                    registrarTentativa: true,
                    desativarDispositivo: true,
                    ct: ct);

                break;

            case SituacaoEnvioPush.FalhaDefinitiva:

                await PersistirResultado(
                    envio,
                    concessao,
                    SituacaoFilaPush.FalhaDefinitiva,
                    tentativas,
                    resultado.CodigoErro,
                    registrarTentativa: true,
                    ct: ct);

                break;

            case SituacaoEnvioPush.ErroConfiguracao:

                // Não consome o limite de tentativas por um erro de configuração.
                // O envio continua sujeito à expiração.

                await PersistirResultado(
                    envio,
                    concessao,
                    SituacaoFilaPush.Pendente,
                    envio.Tentativas,
                    resultado.CodigoErro,
                    proximaTentativa:
                        agora.AddHours(1),
                    ct: ct);

                break;

            case SituacaoEnvioPush.Desativado:

                await PersistirResultado(
                    envio,
                    concessao,
                    SituacaoFilaPush.Pendente,
                    envio.Tentativas,
                    "FIREBASE_DESATIVADO",
                    proximaTentativa:
                        agora.AddMinutes(1),
                    ct: ct);

                break;

            case SituacaoEnvioPush.FalhaTemporaria:
            default:

                if (tentativas >= MaximoTentativas)
                {
                    await PersistirResultado(
                        envio,
                        concessao,
                        SituacaoFilaPush.FalhaDefinitiva,
                        tentativas,
                        resultado.CodigoErro ??
                            "LIMITE_TENTATIVAS",
                        registrarTentativa: true,
                        ct: ct);
                }
                else
                {
                    var proxima = agora.Add(
                        CalcularIntervalo(
                            tentativas));

                    await PersistirResultado(
                        envio,
                        concessao,
                        SituacaoFilaPush.Pendente,
                        tentativas,
                        resultado.CodigoErro ??
                            "FALHA_TEMPORARIA",
                        proximaTentativa: proxima,
                        registrarTentativa: true,
                        ct: ct);
                }

                break;
        }
    }

    // CALCULAR INTERVALO DE NOVA TENTATIVA
    private static TimeSpan CalcularIntervalo(
        int tentativas)
    {
        var minutos = tentativas switch
        {
            1 => 1,

            2 => 5,

            3 => 15,

            4 => 30,

            _ => 60
        };

        // Pequena variação para evitar que vários envios sejam retomados exatamente no mesmo instante.

        var variacao =
            Random.Shared.Next(0, 16);

        return TimeSpan.FromMinutes(minutos)
            .Add(TimeSpan.FromSeconds(variacao));
    }

    // PERSISTIR RESULTADO COM SEGURANÇA
    private async Task PersistirResultado(
        EnvioPush envio,
        Guid concessao,
        SituacaoFilaPush situacao,
        int tentativas,
        string? codigoErro = null,
        string? mensagemFirebaseId = null,
        DateTime? proximaTentativa = null,
        bool registrarTentativa = false,
        bool desativarDispositivo = false,
        CancellationToken ct = default)
    {
        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var novaVersao =
            Guid.NewGuid();

        var finalizado =
            situacao == SituacaoFilaPush.Pendente
                ? (DateTime?)null
                : agora;

        var ultimaTentativa =
            registrarTentativa
                ? agora
                : envio.UltimaTentativaEmUtc;

        var proxima =
            proximaTentativa ??
            envio.ProximaTentativaEmUtc;

        codigoErro =
            LimitarTexto(codigoErro, 100);

        mensagemFirebaseId =
            LimitarTexto(mensagemFirebaseId, 500);

        // A transação não inclui a comunicação externa com o Firebase.

        await using var tx =
            await db.Database
                .BeginTransactionAsync(ct);

        var alterados =
            await db.EnviosPush
                .Where(e =>
                    e.Id == envio.Id &&

                    e.Concessao == concessao &&

                    e.Versao == envio.Versao &&

                    e.ProcessandoAteUtc > agora &&

                    e.Situacao ==
                        SituacaoFilaPush.Pendente)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(
                            e => e.Situacao,
                            situacao)
                        .SetProperty(
                            e => e.Tentativas,
                            tentativas)
                        .SetProperty(
                            e => e.UltimoCodigoErro,
                            codigoErro)
                        .SetProperty(
                            e => e.MensagemFirebaseId,
                            mensagemFirebaseId)
                        .SetProperty(
                            e => e.UltimaTentativaEmUtc,
                            ultimaTentativa)
                        .SetProperty(
                            e => e.ProximaTentativaEmUtc,
                            proxima)
                        .SetProperty(
                            e => e.FinalizadoEmUtc,
                            finalizado)
                        .SetProperty(
                            e => e.Concessao,
                            (Guid?)null)
                        .SetProperty(
                            e => e.ProcessandoAteUtc,
                            (DateTime?)null)
                        .SetProperty(
                            e => e.Versao,
                            novaVersao),
                    ct);

        if (alterados != 1)
        {
            // Outra instância pode ter assumido o processamento da notificação.

            await tx.RollbackAsync(ct);

            return;
        }

        // DESATIVAR TOKEN INVÁLIDO

        if (desativarDispositivo)
        {
            var novaVersaoDispositivo =
                Guid.NewGuid();

            // Somente desativa se o dispositivo continua com a associação original.
            // Uma resposta atrasada do Firebase não deve desativar um dispositivo que já foi registrado novamente.

            await db.DispositivosPush
                .Where(d =>
                    d.Id == envio.DispositivoId &&

                    d.Ativo &&

                    d.UsuarioId == envio.UsuarioId &&

                    d.Versao ==
                        envio.VersaoDispositivo &&

                    d.VersaoSessaoUsuario ==
                        envio.VersaoSessaoUsuario)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(
                            d => d.Ativo,
                            false)
                        .SetProperty(
                            d => d.DesativadoEmUtc,
                            (DateTime?)agora)
                        .SetProperty(
                            d => d.AtualizadoEmUtc,
                            agora)
                        .SetProperty(
                            d => d.Versao,
                            novaVersaoDispositivo),
                    ct);
        }

        await tx.CommitAsync(ct);
    }

    // RESPEITAR LIMITES DO BANCO
    private static string? LimitarTexto(
        string? valor,
        int tamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Length <= tamanho
            ? valor
            : valor[..tamanho];
    }
}