using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Models.Salao;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public class AvisosSalaoService(AppDbContext db,TimeProvider relogio)
{
    public async Task Pronto(AtendimentoMesa a,ItemComanda item,CancellationToken ct)
    {
        var ids=await db.Usuarios.Where(u=>u.Ativo&&(u.Id==a.GarcomId||u.Tipo==TipoUsuario.Gerente||u.Tipo==TipoUsuario.Administrador)).Select(u=>u.Id).ToListAsync(ct);
        Criar(ids,TipoNotificacao.ItemSalaoPronto,$"{a.NomeMesa}: item pronto",$"Retirar {item.Quantidade} × {item.NomeProduto}. Observações: {item.Observacao??"Sem observações"}.",a.Id);
    }
    public async Task Correcao(AtendimentoMesa a,ItemComanda item,string motivo,CancellationToken ct)
    {
        var ids=await db.Usuarios.Where(u=>u.Ativo&&(u.Tipo==TipoUsuario.Cozinha||u.Tipo==TipoUsuario.Gerente||u.Tipo==TipoUsuario.Administrador)).Select(u=>u.Id).ToListAsync(ct);
        Criar(ids,TipoNotificacao.CorrecaoCozinha,$"{a.NomeMesa}: correção no pedido",$"Interrompa/confira {item.Quantidade} × {item.NomeProduto}. Motivo: {motivo}. A fila já mostra o item cancelado e, quando houver, sua substituição.",a.Id);
    }
    public async Task Transferencia(AtendimentoMesa a,CancellationToken ct)
    {
        await Task.CompletedTask;
        Criar([a.GarcomId],TipoNotificacao.MesaTransferida,$"Você assumiu a {a.NomeMesa}","Confira a comanda, os itens prontos para retirada e as observações do atendimento.",a.Id);
    }
    private void Criar(List<int> ids,TipoNotificacao tipo,string titulo,string mensagem,Guid atendimento)
    {
        var n=new Notificacao{Tipo=tipo,Publico=PublicoNotificacao.Funcionario,Titulo=titulo,Mensagem=mensagem,AtendimentoId=atendimento,CriadaEmUtc=relogio.GetUtcNow().UtcDateTime};
        db.Notificacoes.Add(n);db.NotificacoesUsuarios.AddRange(ids.Distinct().Select(id=>new NotificacaoUsuario{Notificacao=n,UsuarioId=id}));
    }
}
