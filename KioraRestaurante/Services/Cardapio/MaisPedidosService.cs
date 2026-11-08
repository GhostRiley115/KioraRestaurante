using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Cardapio;

public sealed record RankingMaisPedidos(
    PeriodoMaisPedidos Periodo,
    IReadOnlyDictionary<int, long> Quantidades);

public class MaisPedidosService(AppDbContext context, TimeProvider relogio)
{
    public async Task<RankingMaisPedidos> Consultar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var periodo = PeriodoMaisPedidos.Atual(agora);
        var agoraUtc = agora.UtcDateTime;

        // Conta unidades vendidas; não acessos à página nem itens de carrinho.
        // DataPedido é armazenada em UTC no fluxo atual de criação de pedidos.
        var quantidades = await context.ItensPedido.AsNoTracking()
            .Where(i => i.Pedido.DataPedido >= periodo.InicioUtc
                        && i.Pedido.DataPedido < periodo.FimUtc
                        && i.Pedido.DataPedido <= agoraUtc
                        && i.Pedido.StatusPagamento == StatusPagamento.Aprovado
                        && i.Pedido.StatusPedido != StatusPedido.Cancelado
                        && i.Produto.Ativo
                        && i.Produto.Categoria.Ativa)
            .GroupBy(i => i.ProdutoId)
            .Select(g => new
            {
                ProdutoId = g.Key,
                Quantidade = g.Sum(i => (long)i.Quantidade)
            })
            .ToDictionaryAsync(x => x.ProdutoId, x => x.Quantidade, ct);

        return new RankingMaisPedidos(periodo, quantidades);
    }
}