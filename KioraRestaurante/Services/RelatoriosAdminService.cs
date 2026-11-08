using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services;

// Relatório gerencial por data do pedido (delivery) e fechamento (salão), não conciliação bancária.
public class RelatoriosAdminService(AppDbContext db, TimeProvider relogio)
{
    public async Task<RelatorioAdminDTO> Obter(int administradorId, FiltroRelatorioAdmin filtro, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u=>u.Id==administradorId && u.Ativo && u.Tipo==TipoUsuario.Administrador,ct))
            throw new UnauthorizedAccessException();
        Validator.ValidateObject(filtro, new(filtro), true);
        var hoje = HorarioRestaurante.ParaLocal(relogio.GetUtcNow().UtcDateTime).Date;
        filtro.Inicio ??= new DateTime(hoje.Year,hoje.Month,1);
        filtro.Fim ??= hoje;
        filtro.CompararInicio ??= filtro.Inicio.Value.AddMonths(-1);
        filtro.CompararFim ??= filtro.Fim.Value.AddMonths(-1);
        Validator.ValidateObject(filtro, new(filtro), true);
        var inicio = HorarioRestaurante.InicioDiaUtc(filtro.Inicio.Value);
        var fim = HorarioRestaurante.InicioDiaUtc(filtro.Fim.Value.AddDays(1));
        var anteriorInicio = HorarioRestaurante.InicioDiaUtc(filtro.CompararInicio.Value);
        var anteriorFim = HorarioRestaurante.InicioDiaUtc(filtro.CompararFim.Value.AddDays(1));
        var vendas = new List<VendaAdminDTO>();
        if (filtro.Canal != "salao")
        {
            // Revisões e ajustes financeiros ainda em aberto não são receita confirmada.
            vendas.AddRange(await db.Pedidos.AsNoTracking().Where(p=>
                ((p.DataPedido>=inicio && p.DataPedido<fim) || (p.DataPedido>=anteriorInicio && p.DataPedido<anteriorFim))
                && p.StatusPedido!=StatusPedido.Cancelado && p.OperacaoPendenteId==null
                && (p.StatusPagamento==StatusPagamento.Aprovado || (p.FormaPagamento==FormaPagamento.Dinheiro
                    && p.StatusPedido==StatusPedido.Entregue && p.StatusPagamento==StatusPagamento.Pendente)))
                .Select(p=>new VendaAdminDTO { Canal="delivery", Identificacao="#"+p.Id, PedidoId=p.Id, DataUtc=p.DataPedido,
                    Total=p.ValorTotal, Frete=p.TaxaEntrega, DescontoPedido=p.DescontoCupom,
                    Itens=p.ItensPedido.Select(i=>new ItemVendaAdminDTO(i.ProdutoId,i.NomeProduto,i.Quantidade,i.PrecoUnitario,i.PrecoOriginalUnitario)).ToList()
                }).ToListAsync(ct));
        }
        if (filtro.Canal != "delivery")
        {
            vendas.AddRange(await db.AtendimentosMesa.AsNoTracking().Where(a=>
                (a.Situacao==SituacaoAtendimento.AguardandoLimpeza || a.Situacao==SituacaoAtendimento.Encerrado)
                && ((a.FechadoEmUtc>=inicio && a.FechadoEmUtc<fim) || (a.FechadoEmUtc>=anteriorInicio && a.FechadoEmUtc<anteriorFim)))
                .Select(a=>new VendaAdminDTO { Canal="salao", Identificacao=a.NomeMesa, AtendimentoId=a.Id, DataUtc=a.FechadoEmUtc!.Value,
                    Total=a.TotalFechamento, DescontoPedido=a.SubtotalFechamento-a.TotalFechamento,
                    Itens=a.Itens.Where(i=>i.Situacao!=SituacaoPreparo.Cancelado)
                        .Select(i=>new ItemVendaAdminDTO(i.ProdutoId,i.NomeProduto,i.Quantidade,i.PrecoUnitario,i.PrecoOriginalUnitario)).ToList()
                }).ToListAsync(ct));
        }
        var atuais=vendas.Where(v=>v.DataUtc>=inicio && v.DataUtc<fim).OrderByDescending(v=>v.DataUtc).ThenBy(v=>v.PedidoId).ThenBy(v=>v.AtendimentoId).ToList();
        var anteriores=vendas.Where(v=>v.DataUtc>=anteriorInicio && v.DataUtc<anteriorFim).ToList();
        var r=new RelatorioAdminDTO { Filtros=filtro, GeradoEmUtc=relogio.GetUtcNow().UtcDateTime,
            Atual=Resumir(atuais), Anterior=Resumir(anteriores), Vendas=atuais };
        var porDia=atuais.GroupBy(v=>HorarioRestaurante.ParaLocal(v.DataUtc).Date).ToDictionary(g=>g.Key,g=>g.ToList());
        for(var dia=filtro.Inicio.Value.Date;dia<=filtro.Fim.Value.Date;dia=dia.AddDays(1))
        {
            var lista=porDia.GetValueOrDefault(dia)??[];
            r.Dias.Add(new(dia,lista.Where(v=>v.Canal=="delivery").Sum(v=>v.Total),lista.Where(v=>v.Canal=="salao").Sum(v=>v.Total)));
        }
        r.Produtos=atuais.SelectMany(v=>v.Itens.Select(i=>new { Item=i, v.Canal }))
            .GroupBy(x=>x.Item.ProdutoId).Select(g=>new ProdutoVendidoAdminDTO(g.Key,g.First().Item.Nome,
                g.Sum(x=>x.Item.Quantidade),g.Where(x=>x.Canal=="delivery").Sum(x=>x.Item.Quantidade),
                g.Where(x=>x.Canal=="salao").Sum(x=>x.Item.Quantidade),g.Where(x=>x.Item.Original>x.Item.Preco).Sum(x=>x.Item.Quantidade),
                g.Sum(x=>x.Item.Quantidade*x.Item.Preco))).OrderByDescending(p=>p.Quantidade).ThenBy(p=>p.Nome).ToList();
        // A procura por reservas pertence ao restaurante físico e não depende do canal financeiro selecionado.
        var reservas=await db.Reservas.AsNoTracking().Where(a=>a.Tipo==KioraRestaurante.Models.TipoReserva.Agendada && a.InicioUtc>=inicio && a.InicioUtc<fim)
            .Select(a=>new { a.InicioUtc, a.Situacao }).ToListAsync(ct);
        var validas=reservas.Where(a=>a.Situacao!=KioraRestaurante.Models.SituacaoReserva.Cancelada && a.Situacao!=KioraRestaurante.Models.SituacaoReserva.Expirada).ToList();
        r.ReservasPeriodo=validas.Count;r.ReservasCanceladas=reservas.Count-validas.Count;
        r.Horas=Enumerable.Range(0,24).Select(h=>new HoraMovimentoAdminDTO(h,
            atuais.Count(v=>v.Canal=="delivery" && HorarioRestaurante.ParaLocal(v.DataUtc).Hour==h),
            atuais.Count(v=>v.Canal=="salao" && HorarioRestaurante.ParaLocal(v.DataUtc).Hour==h),
            validas.Count(a=>HorarioRestaurante.ParaLocal(a.InicioUtc!.Value).Hour==h))).ToList();
        var ativos=await db.Pedidos.AsNoTracking().Where(p=>p.StatusPedido==StatusPedido.Recebido || p.StatusPedido==StatusPedido.EmPreparo || p.StatusPedido==StatusPedido.SaiuParaEntrega)
            .Select(p=>new { p.StatusPedido,p.FormaPagamento,p.StatusPagamento,p.RevisaoFinanceira,p.OperacaoPendenteId }).ToListAsync(ct);

        r.DeliveryNaFila=ativos.Count(p=>p.StatusPedido==StatusPedido.Recebido && (p.OperacaoPendenteId==null && (p.StatusPagamento==StatusPagamento.Aprovado || (p.FormaPagamento==FormaPagamento.Dinheiro && p.StatusPagamento==StatusPagamento.Pendente))));
        r.DeliveryEmPreparo=ativos.Count(p=>p.StatusPedido==StatusPedido.EmPreparo);
        r.DeliveryEmEntrega=ativos.Count(p=>p.StatusPedido==StatusPedido.SaiuParaEntrega);
        r.DeliveryAguardandoPagamento=ativos.Count(p=>p.StatusPedido==StatusPedido.Recebido && !(p.OperacaoPendenteId==null && (p.StatusPagamento==StatusPagamento.Aprovado || (p.FormaPagamento==FormaPagamento.Dinheiro && p.StatusPagamento==StatusPagamento.Pendente))));
        r.RevisoesFinanceiras=await db.Pedidos.CountAsync(p=>p.StatusPagamento==StatusPagamento.Conferencia || p.ReembolsoCancelamentoPendente,ct);
        r.MesasOcupadas=await db.AtendimentosMesa.CountAsync(a=>a.MesaOcupadaId!=null && a.Situacao==SituacaoAtendimento.Aberto,ct);
        r.MesasLimpeza=await db.AtendimentosMesa.CountAsync(a=>a.MesaOcupadaId!=null && a.Situacao==SituacaoAtendimento.AguardandoLimpeza,ct);
        var agora=relogio.GetUtcNow().UtcDateTime;
        r.MesasLivres=await db.MesasRestaurante.CountAsync(m=>m.Ativa && !db.AtendimentosMesa.Any(a=>a.MesaOcupadaId==m.Id)
            && !db.Reservas.Any(a=>a.MesaId==m.Id && a.Situacao==KioraRestaurante.Models.SituacaoReserva.Chamada && a.ExpiraEmUtc>agora),ct);
        r.GruposNaFila=await db.Reservas.CountAsync(a=>a.Situacao==KioraRestaurante.Models.SituacaoReserva.Aguardando || a.Situacao==KioraRestaurante.Models.SituacaoReserva.Chamada,ct);
        return r;
    }
    private static ResumoVendasAdminDTO Resumir(List<VendaAdminDTO> vendas)
    {
        var itens=vendas.SelectMany(v=>v.Itens).ToList();
        return new() { Delivery=vendas.Where(v=>v.Canal=="delivery").Sum(v=>v.Total), Salao=vendas.Where(v=>v.Canal=="salao").Sum(v=>v.Total),
            Quantidade=vendas.Count, Frete=vendas.Sum(v=>v.Frete), Cupons=vendas.Where(v=>v.Canal=="delivery").Sum(v=>v.DescontoPedido),
            DescontosComanda=vendas.Where(v=>v.Canal=="salao").Sum(v=>v.DescontoPedido),
            PedidosComCupom=vendas.Count(v=>v.Canal=="delivery" && v.DescontoPedido>0),
            DescontoCatalogo=itens.Sum(i=>Math.Max(0,(i.Original??i.Preco)-i.Preco)*i.Quantidade),
            UnidadesPromocionais=itens.Where(i=>i.Original>i.Preco).Sum(i=>i.Quantidade),
            UnidadesSemHistoricoDesconto=itens.Where(i=>i.Original==null).Sum(i=>i.Quantidade) };
    }
}
