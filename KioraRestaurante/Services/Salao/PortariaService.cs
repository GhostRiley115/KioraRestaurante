using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public record GrupoSalaoDTO(Guid Codigo,string Nome,int Pessoas,string Tipo,string Situacao,int? MesaId,DateTime? HorarioUtc,DateTime? ChegadaUtc,Guid Versao);
public class PortariaService(AppDbContext db,AcessoSalaoService acesso,ComandoSalaoService comandos,HorariosReserva horarios)
{
    private void Evento(Reserva r,string descricao,int usuarioId)
    {r.Versao=Guid.NewGuid();r.Eventos.Add(new(){AdministradorId=usuarioId,DataUtc=horarios.Agora,Descricao=descricao});}
    public async Task<List<GrupoSalaoDTO>> Grupos(int usuarioId,DateOnly? dia=null,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Portaria,TipoUsuario.Garcom,TipoUsuario.Gerente);
        var data=dia??horarios.Hoje;if(data.Year<2020||data>horarios.Hoje.AddDays(horarios.Regras.AntecedenciaDias))throw new ValidationException("Escolha uma data válida.");
        var inicio=HorariosReserva.Utc(data,0);var fim=inicio.AddDays(1);
        var lista=await db.Reservas.AsNoTracking().Where(r=>(r.InicioUtc>=inicio&&r.InicioUtc<fim||r.Tipo==TipoReserva.Fila&&r.CriadaEmUtc>=inicio&&r.CriadaEmUtc<fim)
            &&(r.Situacao==SituacaoReserva.AguardandoChegada||r.Situacao==SituacaoReserva.Confirmada||r.Situacao==SituacaoReserva.Aguardando||r.Situacao==SituacaoReserva.Chamada))
            .OrderBy(r=>r.ChegadaEmUtc??r.InicioUtc??r.CriadaEmUtc).ThenBy(r=>r.Id).Take(300).ToListAsync(ct);
        return lista.Select(r=>new GrupoSalaoDTO(r.Codigo,r.Nome,r.Pessoas,r.Tipo.ToString(),HorariosReserva.Texto(r.Situacao),r.MesaId,r.InicioUtc.HasValue?DateTime.SpecifyKind(r.InicioUtc.Value,DateTimeKind.Utc):null,r.ChegadaEmUtc.HasValue?DateTime.SpecifyKind(r.ChegadaEmUtc.Value,DateTimeKind.Utc):null,r.Versao)).ToList();
    }
    public async Task<Guid> Fila(int usuarioId,FilaPresencialDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Portaria,TipoUsuario.Gerente);Validator.ValidateObject(dto,new(dto),true);
        var telefone=new string((dto.Telefone??"").Where(char.IsAsciiDigit).ToArray());
        if(telefone.Length is not (0 or 10 or 11))throw new ValidationException("Confira o telefone com DDD ou deixe-o em branco.");
        return (await comandos.Executar(usuarioId,"fila-presencial",dto.ChaveSolicitacao,dto,async()=>
        {
            horarios.ExigirFilaAberta();
            if(!await db.MesasRestaurante.AnyAsync(m=>m.Ativa&&m.Capacidade>=dto.Pessoas,ct))throw new ValidationException("Não há mesa habilitada que comporte esse grupo. Consulte o gerente antes de incluir na fila.");
            var r=new Reserva{ChaveSolicitacao=dto.ChaveSolicitacao,Nome=dto.Nome.Trim(),Telefone=telefone,Pessoas=dto.Pessoas,Tipo=TipoReserva.Fila,
                Situacao=SituacaoReserva.Aguardando,ChegadaEmUtc=horarios.Agora,CriadaEmUtc=horarios.Agora,ExpiraEmUtc=HorariosReserva.Utc(horarios.Hoje,horarios.Regras.FechamentoMinutos)};
            Evento(r,"Grupo registrado presencialmente pela portaria; aguardando mesa.",usuarioId);db.Reservas.Add(r);await Task.CompletedTask;
            return new(r.Codigo,null,$"Grupo de {r.Pessoas} pessoa(s) incluído na fila presencial.");
        },ct))!.Value;
    }
    public async Task Chegada(int usuarioId,ChegadaReservaDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Portaria,TipoUsuario.Gerente);
        await comandos.Executar(usuarioId,"chegada-portaria",dto.ChaveSolicitacao,dto,async()=>
        {
            horarios.ExigirFilaAberta();
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct)??throw new KeyNotFoundException();
            if(r.Versao!=dto.Versao)throw new ValidationException("A reserva mudou. Atualize antes de registrar a chegada.");
            if(r.Situacao is not (SituacaoReserva.AguardandoChegada or SituacaoReserva.Confirmada))throw new ValidationException("Esta solicitação não está aguardando chegada.");
            if(r.ExpiraEmUtc<=horarios.Agora||r.InicioUtc>horarios.Agora.AddMinutes(15))throw new ValidationException("Confira o horário e a tolerância de chegada da reserva.");
            r.ChegadaEmUtc=horarios.Agora;r.Situacao=SituacaoReserva.Aguardando;r.MesaId=null;r.FimUtc=null;
            r.ExpiraEmUtc=HorariosReserva.Utc(horarios.Hoje,horarios.Regras.FechamentoMinutos);Evento(r,"Chegada registrada pela portaria; aguardando acomodação pelo garçom.",usuarioId);
            return new(r.Codigo,null,"Chegada registrada, sem ativar mesa.");
        },ct);
    }
    public async Task Chamar(int usuarioId,int mesaId,Guid chave,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Portaria,TipoUsuario.Gerente);
        await comandos.Executar(usuarioId,"chamar-grupo",chave,new{mesaId},async()=>
        {
            horarios.ExigirFilaAberta();
            var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==mesaId&&m.Ativa,ct)??throw new ValidationException("Mesa indisponível.");
            if(await db.AtendimentosMesa.AnyAsync(a=>a.MesaOcupadaId==mesaId,ct)||await db.Reservas.AnyAsync(r=>r.MesaId==mesaId&&r.Situacao==SituacaoReserva.Chamada&&r.ExpiraEmUtc>horarios.Agora,ct))throw new ValidationException("Mesa ocupada, aguardando limpeza ou já destinada a um grupo chamado.");
            var grupo=await db.Reservas.Where(r=>r.Situacao==SituacaoReserva.Aguardando&&r.ChegadaEmUtc!=null&&r.ExpiraEmUtc>horarios.Agora&&r.Pessoas<=mesa.Capacidade)
                .OrderBy(r=>r.ChegadaEmUtc).ThenBy(r=>r.Id).FirstOrDefaultAsync(ct)??throw new ValidationException("Não há grupo aguardando que caiba nesta mesa.");
            grupo.Situacao=SituacaoReserva.Chamada;grupo.MesaId=mesaId;
            grupo.ExpiraEmUtc=horarios.Agora.AddMinutes(horarios.Regras.ChamadaMinutos);Evento(grupo,$"Grupo chamado para a {mesa.Nome}; aguardando acomodação pelo garçom.",usuarioId);
            return new(grupo.Codigo,null,$"Grupo chamado para {mesa.Nome}; mesa ainda não ativada.");
        },ct);
    }
    public async Task Cancelar(int usuarioId,OperarReservaDTO dto,Guid chave,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Gerente);Validator.ValidateObject(dto,new(dto),true);
        if(string.IsNullOrWhiteSpace(dto.Motivo))throw new ValidationException("Informe o motivo do cancelamento.");
        await comandos.Executar(usuarioId,"cancelar-reserva",chave,dto,async()=>
        {
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct)??throw new KeyNotFoundException();
            if(r.Versao!=dto.Versao)throw new ValidationException("A reserva mudou. Atualize a tela.");
            if(r.Situacao is SituacaoReserva.EmAtendimento or SituacaoReserva.Concluida or SituacaoReserva.Cancelada or SituacaoReserva.Expirada)throw new ValidationException("A solicitação não pode ser cancelada neste estado.");
            r.Situacao=SituacaoReserva.Cancelada;Evento(r,"Cancelada pela gerência: "+dto.Motivo.Trim(),usuarioId);
            return new(r.Codigo,null,"Reserva cancelada com motivo: "+dto.Motivo.Trim());
        },ct);
    }
    public async Task ConfigurarMesa(int usuarioId,ConfigurarMesaDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Gerente);Validator.ValidateObject(dto,new(dto),true);
        await comandos.Executar(usuarioId,"configurar-mesa",dto.ChaveSolicitacao,dto,async()=>
        {
            var mesa=dto.MesaId==0?new MesaRestaurante():await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==dto.MesaId,ct)??throw new KeyNotFoundException();
            if(dto.MesaId!=0&&mesa.Versao!=dto.Versao)throw new ValidationException("A mesa mudou. Abra a edição novamente.");
            if(dto.MesaId!=0&&(await db.AtendimentosMesa.AnyAsync(a=>a.MesaOcupadaId==mesa.Id,ct)||await db.Reservas.AnyAsync(r=>r.MesaId==mesa.Id&&r.Situacao==SituacaoReserva.Chamada&&r.ExpiraEmUtc>horarios.Agora,ct)))throw new ValidationException("Libere o atendimento ou a chamada antes de alterar a configuração da mesa.");
            if(await db.MesasRestaurante.AnyAsync(m=>m.Id!=mesa.Id&&m.Nome==dto.Nome.Trim(),ct))throw new ValidationException("Já existe uma mesa com esse nome.");
            mesa.Nome=dto.Nome.Trim();mesa.Capacidade=dto.Capacidade;mesa.Ativa=dto.Ativa;mesa.Versao=Guid.NewGuid();if(dto.MesaId==0)db.MesasRestaurante.Add(mesa);
            await Task.CompletedTask;return new(null,null,$"Mesa configurada: {mesa.Nome}; capacidade {mesa.Capacidade}; habilitada {mesa.Ativa}.");
        },ct);
    }
}
