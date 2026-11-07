using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Pedidos;

public class AndamentoPedidoService(AppDbContext db)
{
    public async Task Avancar(int adminId, int id, Guid versao, StatusPedido proximo, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == adminId && u.Ativo && u.Tipo == TipoUsuario.Administrador, ct)) throw new UnauthorizedAccessException();
        var pedido = await db.Pedidos.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new KeyNotFoundException();
        if (pedido.Versao != versao) throw new ValidationException("O pedido mudou. Atualize os detalhes antes de avançar.");
        if (pedido.OperacaoPendenteId.HasValue) throw new ValidationException("Conclua a solicitação pendente antes de avançar o pedido.");
        if (pedido.StatusPedido is StatusPedido.Cancelado or StatusPedido.Entregue || (int)proximo != (int)pedido.StatusPedido + 1
            || proximo is < StatusPedido.EmPreparo or > StatusPedido.Entregue)
            throw new ValidationException("Avance uma etapa por vez, conforme o andamento real do pedido.");
        if (pedido.FormaPagamento != FormaPagamento.Dinheiro && pedido.StatusPagamento != StatusPagamento.Aprovado)
            throw new ValidationException("Confirme o pagamento antes de avançar este pedido.");
        if (pedido.FormaPagamento == FormaPagamento.Dinheiro && pedido.StatusPagamento is not StatusPagamento.Pendente and not StatusPagamento.Aprovado)
            throw new ValidationException("A situação do pagamento precisa ser conferida antes de avançar.");
        pedido.StatusPedido = proximo; pedido.Versao = Guid.NewGuid();
        if (proximo == StatusPedido.Entregue && pedido.FormaPagamento == FormaPagamento.Dinheiro)
            pedido.StatusPagamento = StatusPagamento.Aprovado;
        await db.SaveChangesAsync(ct);
    }
}
