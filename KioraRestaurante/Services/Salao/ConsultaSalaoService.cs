using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public class ConsultaSalaoService(AppDbContext db,AcessoSalaoService acesso,TimeProvider relogio)
{
    public async Task<List<MesaSalaoDTO>> Mesas(int usuarioId,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente,TipoUsuario.Portaria);
        var mesas=await db.MesasRestaurante.AsNoTracking().OrderBy(m=>m.Id).ToListAsync(ct);
        var atendimentos=await db.AtendimentosMesa.AsNoTracking().Include(a=>a.Garcom).Where(a=>a.MesaOcupadaId!=null).ToListAsync(ct);
        var agora=relogio.GetUtcNow().UtcDateTime;
        var chamadas=await db.Reservas.AsNoTracking().Where(r=>r.Situacao==KioraRestaurante.Models.SituacaoReserva.Chamada&&r.ExpiraEmUtc>agora&&r.MesaId!=null).Select(r=>r.MesaId).ToListAsync(ct);
        return mesas.Select(m=>
        {
            var a=atendimentos.SingleOrDefault(a=>a.MesaOcupadaId==m.Id);
            var situacao=!m.Ativa?"Indisponível":a?.Situacao==SituacaoAtendimento.AguardandoLimpeza?"Aguardando limpeza":a!=null?"Ocupada":chamadas.Contains(m.Id)?"Aguardando garçom":"Livre";
            return new MesaSalaoDTO(m.Id,m.Nome,m.Capacidade,m.Ativa,situacao,m.Versao,a?.Id,a?.Garcom.Nome,a?.GarcomId,a?.Pessoas,a==null?null:DateTime.SpecifyKind(a.AbertoEmUtc,DateTimeKind.Utc));
        }).ToList();
    }
    public async Task<List<FuncionarioOpcaoDTO>> Garcons(int usuarioId,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente,TipoUsuario.Portaria);
        return await db.Usuarios.AsNoTracking().Where(u=>u.Ativo&&(u.Tipo==TipoUsuario.Garcom||u.Tipo==TipoUsuario.Gerente||u.Tipo==TipoUsuario.Administrador))
            .OrderBy(u=>u.Nome).Select(u=>new FuncionarioOpcaoDTO(u.Id,u.Nome,u.Tipo)).ToListAsync(ct);
    }
    public async Task<ComandaDetalhesDTO> Comanda(int usuarioId,Guid id,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);
        var a=await db.AtendimentosMesa.AsNoTracking().Include(a=>a.Garcom).Include(a=>a.Itens).Include(a=>a.Historico).Include(a=>a.Pagamentos)
            .AsSplitQuery().SingleOrDefaultAsync(a=>a.Id==id,ct)??throw new KeyNotFoundException();
        if(!AcessoSalaoService.Gestor(u.Tipo)&&a.GarcomId!=usuarioId)throw new UnauthorizedAccessException();
        var subtotal=a.FechadoEmUtc.HasValue?a.SubtotalFechamento:ComandaService.Subtotal(a);
        var total=a.FechadoEmUtc.HasValue?a.TotalFechamento:subtotal-ComandaService.Desconto(a);
        return new(a.Id,a.MesaId,a.NomeMesa,a.NomeGrupo,a.Pessoas,a.GarcomId,a.Garcom.Nome,a.Situacao,DateTime.SpecifyKind(a.AbertoEmUtc,DateTimeKind.Utc),a.FechadoEmUtc.HasValue?DateTime.SpecifyKind(a.FechadoEmUtc.Value,DateTimeKind.Utc):null,a.Versao,
            subtotal,subtotal-total,total,a.MotivoDesconto,a.Itens.OrderBy(i=>i.EnviadoEmUtc).ThenBy(i=>i.Id).Select(i=>new ItemComandaDTO(i.Id,i.ProdutoId,i.NomeProduto,i.Quantidade,i.PrecoUnitario,
                i.Observacao,i.Composicao,i.ImagemUrl,i.Situacao,DateTime.SpecifyKind(i.EnviadoEmUtc,DateTimeKind.Utc),i.Versao,i.MotivoCancelamento)).ToList(),
            a.Historico.OrderByDescending(h=>h.CriadoEmUtc).Select(h=>new HistoricoSalaoDTO(DateTime.SpecifyKind(h.CriadoEmUtc,DateTimeKind.Utc),h.Responsavel,h.Descricao)).ToList(),
            a.Pagamentos.Select(p=>new RecebimentoPresencialDTO{Meio=p.Meio,Valor=p.Valor,Referencia=p.Referencia}).ToList());
    }
    public async Task<ComparacaoSalaoDTO> Relatorio(int usuarioId,DateOnly dia,DateOnly comparar,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Gerente);
        var hoje=DateOnly.FromDateTime(HorariosReserva.Local(relogio.GetUtcNow().UtcDateTime));
        if(dia.Year<2020||dia>hoje||comparar.Year<2020||comparar>hoje)throw new ValidationException("Escolha datas válidas até hoje.");
        return new(await Dia(dia,ct),await Dia(comparar,ct));
    }
    private async Task<TotaisDiaSalaoDTO> Dia(DateOnly data,CancellationToken ct)
    {
        var inicio=HorariosReserva.Utc(data,0);var fim=inicio.AddDays(1);
        var abertos=await db.AtendimentosMesa.AsNoTracking().Where(a=>a.AbertoEmUtc>=inicio&&a.AbertoEmUtc<fim).Select(a=>a.MesaId).ToListAsync(ct);
        var usados=await db.AtendimentosMesa.AsNoTracking().Where(a=>a.AbertoEmUtc<fim && (a.LiberadoEmUtc==null || a.LiberadoEmUtc>=inicio)).Select(a=>a.MesaId).ToListAsync(ct);
        var encerradas=await db.AtendimentosMesa.AsNoTracking().Where(a=>a.FechadoEmUtc>=inicio&&a.FechadoEmUtc<fim)
            .OrderByDescending(a=>a.FechadoEmUtc).Select(a=>new ResumoContaSalaoDTO(a.Id,a.NomeMesa,a.GarcomFechamento??a.Garcom.Nome,DateTime.SpecifyKind(a.FechadoEmUtc!.Value,DateTimeKind.Utc),
                a.SubtotalFechamento,a.SubtotalFechamento-a.TotalFechamento,a.TotalFechamento)).ToListAsync(ct);
        return new(data,abertos.Count,usados.Count,usados.Distinct().Count(),encerradas.Count,encerradas.Sum(a=>a.Subtotal),encerradas.Sum(a=>a.Desconto),encerradas.Sum(a=>a.Total),
            await db.AtendimentosMesa.CountAsync(a=>a.Situacao==SituacaoAtendimento.Aberto,ct),encerradas);
    }
}
