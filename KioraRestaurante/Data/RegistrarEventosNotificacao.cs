using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Data;

internal static class RegistrarEventosNotificacao
{
    public static void Preparar(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var pendentes = db.ChangeTracker.Entries<EventoNotificacao>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        void Pedido(Pedido pedido, TipoNotificacao tipo)
        {
            if (pendentes.Any(e => ReferenceEquals(e.Pedido, pedido) && e.Tipo == tipo)) return;
            var evento = new EventoNotificacao { Pedido = pedido, Tipo = tipo };
            pendentes.Add(evento); db.EventosNotificacao.Add(evento);
        }
        void Reserva(Reserva reserva, TipoNotificacao tipo)
        {
            if (pendentes.Any(e => ReferenceEquals(e.Reserva, reserva) && e.Tipo == tipo)) return;
            var evento = new EventoNotificacao { Reserva = reserva, Tipo = tipo };
            pendentes.Add(evento); db.EventosNotificacao.Add(evento);
        }
        foreach (var entry in db.ChangeTracker.Entries<Pedido>().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                Pedido(entry.Entity, TipoNotificacao.NovoPedidoAdmin);
                Pedido(entry.Entity, TipoNotificacao.PedidoRecebido);
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Property(p => p.StatusPedido).OriginalValue != entry.Entity.StatusPedido)
                {
                    var tipo = entry.Entity.StatusPedido switch
                    {
                        StatusPedido.EmPreparo => TipoNotificacao.PedidoEmPreparo,
                        StatusPedido.SaiuParaEntrega => TipoNotificacao.PedidoSaiuParaEntrega,
                        StatusPedido.Entregue => TipoNotificacao.PedidoEntregue,
                        StatusPedido.Cancelado => TipoNotificacao.PedidoCancelado,
                        _ => TipoNotificacao.ConferirCupomInterno
                    };
                    Pedido(entry.Entity, tipo);
                }
                if (entry.Property(p => p.StatusPagamento).OriginalValue != entry.Entity.StatusPagamento)
                    Pedido(entry.Entity, entry.Entity.StatusPagamento == StatusPagamento.Aprovado
                        && entry.Entity.FormaPagamento == FormaPagamento.MercadoPago
                        ? TipoNotificacao.PagamentoConfirmado : TipoNotificacao.ConferirCupomInterno);
            }
        }
        foreach (var entry in db.ChangeTracker.Entries<Reserva>().ToList())
        {
            if (entry.State == EntityState.Added) Reserva(entry.Entity, TipoNotificacao.NovaReservaAdmin);
            else if (entry.State == EntityState.Modified && entry.Property(r => r.Situacao).OriginalValue != entry.Entity.Situacao)
                Reserva(entry.Entity, TipoNotificacao.ReservaAtualizada);
        }
    }
}
