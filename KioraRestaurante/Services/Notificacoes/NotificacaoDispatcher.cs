using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Services.Cupons;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Notificacoes;

public class NotificacaoDispatcher(AppDbContext db, CupomService cupons, TimeProvider relogio, ILogger<NotificacaoDispatcher>? logger = null)
{
    public async Task<int> Processar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var ids = await db.EventosNotificacao.AsNoTracking().Where(e => e.ProcessadoEmUtc == null
            && (e.ProcessandoAteUtc == null || e.ProcessandoAteUtc < agora)).OrderBy(e => e.CriadoEmUtc)
            .ThenBy(e => e.Id).Select(e => e.Id).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            try { await Distribuir(id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger?.LogError(ex, "Falha ao distribuir o evento {EventoId}; os demais avisos continuarão.", id); }
        }
        return ids.Count;
    }

    public async Task Distribuir(Guid id, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime; var concessao = Guid.NewGuid();
        var n = await db.EventosNotificacao.Where(e => e.Id == id && e.ProcessadoEmUtc == null
            && (e.ProcessandoAteUtc == null || e.ProcessandoAteUtc < agora))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Concessao, concessao)
                .SetProperty(e => e.ProcessandoAteUtc, agora.AddMinutes(3)), ct);
        if (n != 1) return;
        try
        {
            var evento = await db.EventosNotificacao.AsNoTracking().Include(e => e.Pedido).Include(e => e.Reserva).SingleAsync(e => e.Id == id, ct);
            if (evento.PedidoId.HasValue) await cupons.ReconciliarPedido(evento.PedidoId.Value, ct);
            var admin = evento.Tipo is TipoNotificacao.NovoPedidoAdmin or TipoNotificacao.NovaReservaAdmin;
            var tipoUsuario = admin ? TipoUsuario.Administrador : TipoUsuario.Cliente;
            var dono = evento.Pedido?.UsuarioId ?? evento.Reserva?.UsuarioId;
            var usuarios = await db.Usuarios.Where(u => u.Ativo && (u.Tipo == tipoUsuario || evento.Tipo == TipoNotificacao.NovaReservaAdmin && u.Tipo == TipoUsuario.Gerente) && (admin || u.Id == dono)).Select(u => u.Id).ToListAsync(ct);
            var (titulo, mensagem) = Texto(evento);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Só a instância que ainda possui a concessão pode confirmar a distribuição.
            var concluido = await db.EventosNotificacao.Where(e => e.Id == id && e.Concessao == concessao && e.ProcessadoEmUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ProcessadoEmUtc, (DateTime?)agora)
                    .SetProperty(e => e.ProcessandoAteUtc, (DateTime?)null), ct);
            if (concluido == 1 && titulo != null && usuarios.Count > 0 && !await db.Notificacoes.AnyAsync(x => x.Id == id, ct))
            {
                var aviso = new Notificacao { Id = id, Tipo = evento.Tipo, PedidoId = evento.PedidoId, ReservaId = evento.ReservaId,
                    Publico = admin ? PublicoNotificacao.Administrador : PublicoNotificacao.Cliente,
                    Titulo = titulo, Mensagem = mensagem!, CriadaEmUtc = evento.CriadoEmUtc };
                db.Notificacoes.Add(aviso);
                db.NotificacoesUsuarios.AddRange(usuarios.Select(u => new NotificacaoUsuario { Notificacao = aviso, UsuarioId = u }));
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            // Em falha, a próxima execução retoma o evento; nenhuma notificação parcial é confirmada.
            await db.EventosNotificacao.Where(e => e.Id == id && e.Concessao == concessao && e.ProcessadoEmUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ProcessandoAteUtc, (DateTime?)null), CancellationToken.None);
            throw;
        }
    }

    private static (string? Titulo, string? Mensagem) Texto(EventoNotificacao e)
    {
        var pedido = $"Pedido #{e.PedidoId}";
        return e.Tipo switch
        {
            TipoNotificacao.NovoPedidoAdmin => ("Novo pedido recebido", $"{pedido} acabou de chegar. Confira os produtos e a situação do pagamento."),
            TipoNotificacao.PedidoRecebido => ("Pedido recebido", $"Registramos seu pedido #{e.PedidoId}. Acompanhe as próximas etapas nos detalhes."),
            TipoNotificacao.PedidoEmPreparo => ("Seu pedido está em preparo", $"{pedido}: a cozinha começou o preparo."),
            TipoNotificacao.PedidoSaiuParaEntrega => ("Seu pedido saiu para entrega", $"{pedido} está a caminho. Fique atento à chegada."),
            TipoNotificacao.PedidoEntregue => ("Pedido entregue", $"{pedido} foi marcado como entregue. Obrigado por escolher a Kiora!"),
            TipoNotificacao.PedidoCancelado => ("Seu pedido foi cancelado", $"{pedido} foi cancelado. Confira a situação do pagamento nos detalhes; o aviso não confirma um estorno."),
            TipoNotificacao.PagamentoConfirmado => ("Pagamento confirmado", $"O pagamento do pedido #{e.PedidoId} foi aprovado."),
            TipoNotificacao.NovaReservaAdmin => ("Nova reserva recebida", $"Uma nova solicitação de reserva para {e.Reserva?.Pessoas} pessoa(s) chegou. Confira a agenda ou a fila de espera."),
            TipoNotificacao.ReservaAtualizada => ("Sua reserva foi atualizada", "Consulte a situação atual da sua reserva e as orientações do restaurante."),
            _ => (null, null)
        };
    }
}

public class DistribuirNotificacoesService(IServiceScopeFactory escopos, ILogger<DistribuirNotificacoesService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = escopos.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificacaoDispatcher>().Processar(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao distribuir notificações pendentes."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
