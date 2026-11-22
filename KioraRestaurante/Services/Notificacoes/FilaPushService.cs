using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KioraRestaurante.Services.Notificacoes;

// Prepara os registros da fila persistente de notificações push.
// Não realiza comunicação com o Firebase.
// Os registros são gravados junto com a notificação original.
public sealed class FilaPushService(
    AppDbContext db,
    TimeProvider relogio,
    IOptions<FirebaseOptions> options)
{
    private static readonly TimeSpan ValidadeEnvio =
        TimeSpan.FromHours(24);

    // PREPARAR ENVIOS
    public async Task<int> Preparar(
        Notificacao notificacao,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Quando o Firebase estiver desativado, não consulta a tabela de dispositivos nem cria registros na fila.

        if (!options.Value.Ativado)
        {
            return 0;
        }

        ArgumentNullException.ThrowIfNull(
            notificacao);

        // Este método deve ser executado antes do SaveChanges da notificação original.

        if (db.Entry(notificacao).State !=
                EntityState.Added)
        {
            throw new InvalidOperationException(
                "A notificação deve estar pendente " +
                "de gravação para preparar os envios.");
        }

        if (notificacao.Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A notificação não possui " +
                "um identificador válido.");
        }

        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var expiraEm =
            notificacao.CriadaEmUtc.Add(
                ValidadeEnvio);

        // Não prepara notificações antigas que já ultrapassaram sua validade.

        if (expiraEm <= agora)
        {
            return 0;
        }

        // Nesta primeira versão, utiliza somente os públicos já previstos para pedidos, reservas e comunicados.
        // Visitantes e notificações operacionais específicas serão integrados depois.

        if (notificacao.Publico is not
                PublicoNotificacao.Cliente
            and not PublicoNotificacao.Administrador
            and not PublicoNotificacao.Todos)
        {
            return 0;
        }

        // IDENTIFICAR DESTINATÁRIOS

        // Utiliza exclusivamente os destinatários vinculados à notificação no contexto atual.
        // Não permite enviar para contas que apenas pertençam ao mesmo público.

        db.ChangeTracker.DetectChanges();

        var destinatarios =
            db.ChangeTracker
                .Entries<NotificacaoUsuario>()
                .Where(e =>
                    e.State == EntityState.Added &&
                    e.Entity.NotificacaoId ==
                        notificacao.Id)
                .Select(e => e.Entity.UsuarioId)
                .Distinct()
                .ToArray();

        if (destinatarios.Length == 0)
        {
            return 0;
        }

        // LOCALIZAR DISPOSITIVOS AUTORIZADOS

        var publico =
            notificacao.Publico;

        var dispositivos =
            await db.DispositivosPush
                .AsNoTracking()
                .Where(d =>
                    d.Ativo &&

                    d.Versao != Guid.Empty &&

                    d.UsuarioId.HasValue &&

                    destinatarios.Contains(
                        d.UsuarioId.Value) &&

                    d.Usuario != null &&

                    d.Usuario.Ativo &&

                    !d.Usuario.ConvitePendente &&

                    d.VersaoSessaoUsuario.HasValue &&

                    d.VersaoSessaoUsuario.Value !=
                        Guid.Empty &&

                    d.VersaoSessaoUsuario.Value ==
                        d.Usuario.VersaoSessao &&

                    (
                        publico ==
                            PublicoNotificacao.Todos ||

                        (
                            publico ==
                                PublicoNotificacao.Cliente &&
                            d.Usuario.Tipo ==
                                TipoUsuario.Cliente
                        ) ||

                        (
                            publico ==
                                PublicoNotificacao.Administrador &&
                            (
                                d.Usuario.Tipo ==
                                    TipoUsuario.Administrador ||

                                d.Usuario.Tipo ==
                                    TipoUsuario.Gerente
                            )
                        )
                    ))
                .Select(d => new
                {
                    d.Id,
                    d.UsuarioId,
                    d.VisitanteHash,
                    d.VersaoSessaoUsuario,
                    d.Versao
                })
                .ToListAsync(ct);

        if (dispositivos.Count == 0)
        {
            return 0;
        }

        // EVITAR DUPLICIDADES NO CONTEXTO

        // A migration também possui um índice único para NotificacaoId + DispositivoId.

        var existentes =
            db.EnviosPush.Local
                .Where(e =>
                    e.NotificacaoId ==
                        notificacao.Id)
                .Select(e => e.DispositivoId)
                .ToHashSet();

        var quantidade = 0;

        // PREPARAR REGISTROS DA FILA

        foreach (var dispositivo in dispositivos)
        {
            ct.ThrowIfCancellationRequested();

            if (!existentes.Add(
                    dispositivo.Id))
            {
                continue;
            }

            var envio = new EnvioPush
            {
                Id = Guid.NewGuid(),

                NotificacaoId =
                    notificacao.Id,

                Notificacao =
                    notificacao,

                DispositivoId =
                    dispositivo.Id,

                UsuarioId =
                    dispositivo.UsuarioId,

                VisitanteHash =
                    dispositivo.VisitanteHash,

                VersaoSessaoUsuario =
                    dispositivo.VersaoSessaoUsuario,

                VersaoDispositivo =
                    dispositivo.Versao,

                Situacao =
                    SituacaoFilaPush.Pendente,

                Tentativas = 0,

                CriadoEmUtc = agora,

                ProximaTentativaEmUtc =
                    agora,

                ExpiraEmUtc =
                    expiraEm,

                UltimaTentativaEmUtc = null,

                FinalizadoEmUtc = null,

                ProcessandoAteUtc = null,

                Concessao = null,

                UltimoCodigoErro = null,

                MensagemFirebaseId = null,

                Versao = Guid.NewGuid()
            };

            db.EnviosPush.Add(envio);

            quantidade++;
        }

        // IMPORTANTE:
        // Não executamos SaveChangesAsync aqui.
        // O serviço responsável pela notificação realizará a gravação da notificação, dos destinatários e da fila em conjunto.

        return quantidade;
    }
}
