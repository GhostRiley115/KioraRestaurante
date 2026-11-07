using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public class CozinhaService(AppDbContext db,AcessoSalaoService acesso,ComandoSalaoService comandos,AvisosSalaoService avisos,TimeProvider relogio)
{
    private DateTime Agora=>relogio.GetUtcNow().UtcDateTime;
    public async Task<List<ItemCozinhaDTO>> Fila(int usuarioId,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Cozinha,TipoUsuario.Gerente);
        var salao=await db.ItensComanda.AsNoTracking().Where(i=>i.Atendimento.Situacao==SituacaoAtendimento.Aberto
            &&(i.Situacao==SituacaoPreparo.NaFila||i.Situacao==SituacaoPreparo.EmPreparo||i.Situacao==SituacaoPreparo.Pronto))
            .Select(i=>new ItemCozinhaDTO("salao",i.Id,null,i.AtendimentoId,null,i.Atendimento.NomeMesa,i.NomeProduto,i.Quantidade,i.Observacao,i.Composicao,i.Situacao,DateTime.SpecifyKind(i.EnviadoEmUtc,DateTimeKind.Utc),i.Versao)).ToListAsync(ct);
        var delivery=await db.ItensPedido.AsNoTracking().Where(i=>(i.Pedido.StatusPedido==StatusPedido.Recebido||i.Pedido.StatusPedido==StatusPedido.EmPreparo)
            &&i.Pedido.OperacaoPendenteId==null&&(i.Pedido.FormaPagamento==FormaPagamento.Dinheiro||i.Pedido.StatusPagamento==StatusPagamento.Aprovado))
            .Select(i=>new ItemCozinhaDTO("delivery",null,i.Id,null,i.PedidoId,"Delivery #"+i.PedidoId,i.NomeProduto,i.Quantidade,i.Observacao,i.Composicao,i.SituacaoPreparo,DateTime.SpecifyKind(i.Pedido.DataPedido,DateTimeKind.Utc),i.VersaoPreparo)).ToListAsync(ct);
        return salao.Concat(delivery).OrderBy(i=>i.EnviadoEmUtc).ThenBy(i=>i.Nome).ToList();
    }
    public async Task Atualizar(int usuarioId,PreparoItemDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Cozinha,TipoUsuario.Gerente);Validator.ValidateObject(dto,new(dto),true);
        if(dto.Acao is not ("iniciar" or "pronto"))throw new ValidationException("Escolha iniciar preparo ou marcar pronto.");
        if(dto.Origem is not ("salao" or "delivery"))throw new ValidationException("Origem do item inválida.");
        await comandos.Executar(usuarioId,"cozinha-"+dto.Acao,dto.ChaveSolicitacao,dto,async()=>
        {
            if(dto.Origem=="salao")
            {
                if(dto.ItemSalaoId==null||dto.ItemDeliveryId!=null)throw new ValidationException("Confira a identificação do item.");
                var item=await db.ItensComanda.Include(i=>i.Atendimento).SingleOrDefaultAsync(i=>i.Id==dto.ItemSalaoId,ct)??throw new KeyNotFoundException();
                if(item.Versao!=dto.Versao)throw new ValidationException("O item mudou. Atualize a fila da cozinha.");
                if(item.Atendimento.Situacao!=SituacaoAtendimento.Aberto)throw new ValidationException("A comanda não está mais aberta.");
                ExigirEtapa(item.Situacao,dto.Acao);
                item.Versao=Guid.NewGuid();item.Atendimento.Versao=Guid.NewGuid();
                if(dto.Acao=="iniciar"){item.Situacao=SituacaoPreparo.EmPreparo;item.IniciadoEmUtc=Agora;}
                else {item.Situacao=SituacaoPreparo.Pronto;item.ProntoEmUtc=Agora;await avisos.Pronto(item.Atendimento,item,ct);}
                return new(item.AtendimentoId,item.AtendimentoId,$"Cozinha: {item.Quantidade} × {item.NomeProduto} — {item.Situacao}.");
            }
            if(dto.ItemDeliveryId==null||dto.ItemSalaoId!=null)throw new ValidationException("Confira a identificação do item.");
            var entrega=await db.ItensPedido.Include(i=>i.Pedido).SingleOrDefaultAsync(i=>i.Id==dto.ItemDeliveryId,ct)??throw new KeyNotFoundException();
            var p=entrega.Pedido;
            if(entrega.VersaoPreparo!=dto.Versao)throw new ValidationException("O item mudou. Atualize a fila da cozinha.");
            if(p.StatusPedido is not (StatusPedido.Recebido or StatusPedido.EmPreparo)||p.OperacaoPendenteId!=null
                ||p.FormaPagamento!=FormaPagamento.Dinheiro&&p.StatusPagamento!=StatusPagamento.Aprovado)throw new ValidationException("O pedido não está liberado para preparo.");
            ExigirEtapa(entrega.SituacaoPreparo,dto.Acao);entrega.VersaoPreparo=Guid.NewGuid();p.Versao=Guid.NewGuid();
            if(dto.Acao=="iniciar"){entrega.SituacaoPreparo=SituacaoPreparo.EmPreparo;entrega.IniciadoEmUtc=Agora;p.StatusPedido=StatusPedido.EmPreparo;}
            else {entrega.SituacaoPreparo=SituacaoPreparo.Pronto;entrega.ProntoEmUtc=Agora;}
            // O pedido continua em preparo até a expedição; prato pronto não significa entregue ao cliente.
            return new(null,null,$"Delivery #{p.Id}: {entrega.Quantidade} × {entrega.NomeProduto} — {entrega.SituacaoPreparo}.");
        },ct);
    }
    private static void ExigirEtapa(SituacaoPreparo atual,string acao)
    {
        if(acao=="iniciar"&&atual!=SituacaoPreparo.NaFila||acao=="pronto"&&atual!=SituacaoPreparo.EmPreparo)
            throw new ValidationException("O item mudou de etapa. Atualize a fila antes de continuar.");
    }
}
