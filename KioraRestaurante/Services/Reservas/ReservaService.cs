using System.ComponentModel.DataAnnotations;
using System.Data;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Reservas;

public class ReservaService(AppDbContext db,HorariosReserva horarios)
{
    private static bool Aberta(Reserva r)=>r.Situacao is SituacaoReserva.Confirmada or SituacaoReserva.Aguardando or SituacaoReserva.Chamada or SituacaoReserva.EmAtendimento;
    private static bool Pertence(Reserva r,AcessoReserva a)=>(a.UsuarioId.HasValue&&r.UsuarioId==a.UsuarioId)
        || (r.UsuarioId==null&&a.VisitanteHash!=null&&r.VisitanteHash==a.VisitanteHash);
    private async Task ValidarAcesso(AcessoReserva a,CancellationToken ct)
    {
        if(a.UsuarioId.HasValue)
        {
            if(!await db.Usuarios.AnyAsync(u=>u.Id==a.UsuarioId&&u.Ativo,ct)) throw new UnauthorizedAccessException();
        }
        else if(a.VisitanteHash==null) throw new UnauthorizedAccessException();
    }
    private async Task ExigirAdmin(int id,CancellationToken ct)
    {
        if(!await db.Usuarios.AnyAsync(u=>u.Id==id&&u.Ativo&&u.Tipo==TipoUsuario.Administrador,ct)) throw new UnauthorizedAccessException();
    }
    private void Evento(Reserva r,string texto,int? administrador=null)
    {
        r.Versao=Guid.NewGuid();
        r.Eventos.Add(new EventoReserva {DataUtc=horarios.Agora,Descricao=texto,AdministradorId=administrador});
    }
    private async Task Expirar(CancellationToken ct)
    {
        var agora=horarios.Agora;
        var atrasadas=await db.Reservas.Where(r=>(r.Situacao==SituacaoReserva.Confirmada
            ||r.Situacao==SituacaoReserva.Aguardando||r.Situacao==SituacaoReserva.Chamada)&&r.ExpiraEmUtc<=agora).ToListAsync(ct);
        foreach(var r in atrasadas) {r.Situacao=SituacaoReserva.Expirada;Evento(r,"Prazo de chegada/espera encerrado automaticamente.");}
        // Uma mesa ocupada nunca é liberada automaticamente pelo relógio.
    }
    private async Task<T> ComSala<T>(Func<Task<T>> acao,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        var controle=await db.ControlesSala.FromSqlRaw("SELECT * FROM ControlesSala WHERE Id = 1 FOR UPDATE").ToListAsync(ct);
        if(controle.Count!=1) throw new RegraReservaException("A sala ainda não foi configurada. Aplique a migration de reservas.");
        await Expirar(ct);
        await db.SaveChangesAsync(ct);
        var resultado=await acao();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return resultado;
    }
    public Task Manutencao(CancellationToken ct)=>ComSala(async()=>{await Task.CompletedTask;return true;},ct);

    private async Task<List<Reserva>> Bloqueios(CancellationToken ct)=>await db.Reservas.AsNoTracking()
        .Where(r=>r.MesaId!=null&&(r.Situacao==SituacaoReserva.EmAtendimento
            ||((r.Situacao==SituacaoReserva.Confirmada||r.Situacao==SituacaoReserva.Chamada)&&r.ExpiraEmUtc>horarios.Agora)))
        .ToListAsync(ct);
    private bool Livre(int mesa,DateTime inicio,DateTime fim,IEnumerable<Reserva> bloqueios,int? ignorar=null)=>
        !bloqueios.Any(r=>r.Id!=ignorar&&r.MesaId==mesa&&(
            r.Situacao==SituacaoReserva.EmAtendimento&&r.FimUtc<=horarios.Agora
            ||(r.ChegadaEmUtc??r.InicioUtc)<fim&&r.FimUtc>inicio));

    public async Task<List<HorarioReservaDTO>> Horarios(DateOnly data,int pessoas,CancellationToken ct)
    {
        horarios.ValidarData(data);
        if(pessoas<1||pessoas>6) throw new RegraReservaException("Escolha de 1 a 6 pessoas.");
        var mesas=await db.MesasRestaurante.AsNoTracking().Where(m=>m.Ativa&&m.Capacidade>=pessoas).ToListAsync(ct);
        var bloqueios=await Bloqueios(ct);var regras=horarios.Regras;
        var lista=new List<HorarioReservaDTO>();
        for(var minuto=regras.AberturaMinutos;minuto<=regras.UltimaReservaMinutos;minuto+=regras.IntervaloMinutos)
        {
            var inicio=HorariosReserva.Utc(data,minuto);if(inicio<horarios.Agora) continue;
            var fim=HorariosReserva.Utc(data,Math.Min(minuto+regras.PermanenciaMinutos,regras.FechamentoMinutos));
            lista.Add(new(HorariosReserva.Hora(minuto),HorariosReserva.Local(fim).ToString("HH:mm"),(int)(fim-inicio).TotalMinutes,
                mesas.Any(m=>Livre(m.Id,inicio,fim,bloqueios))));
        }
        return lista;
    }

    public async Task<ReservaResponseDTO> Criar(AcessoReserva acesso,CriarReservaDTO dto,bool fila,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        Validator.ValidateObject(dto,new ValidationContext(dto),true);
        if(dto.ChaveSolicitacao==Guid.Empty) throw new RegraReservaException("Atualize o formulário antes de enviar.");
        var telefone=new string(dto.Telefone.Where(char.IsAsciiDigit).ToArray());
        if(telefone.Length is not (10 or 11)) throw new RegraReservaException("Informe o telefone com DDD (10 ou 11 números).");
        if(string.IsNullOrWhiteSpace(dto.Nome)||dto.Nome.Trim().Length<2) throw new RegraReservaException("Informe seu nome.");
        return await ComSala(async()=>
        {
            var anterior=await db.Reservas.SingleOrDefaultAsync(r=>r.ChaveSolicitacao==dto.ChaveSolicitacao,ct);
            if(anterior!=null)
            {
                if(!Pertence(anterior,acesso)) throw new KeyNotFoundException();
                return await Resposta(anterior,ct);
            }
            var ativas=await db.Reservas.Where(r=>r.Situacao==SituacaoReserva.Confirmada||r.Situacao==SituacaoReserva.Aguardando
                ||r.Situacao==SituacaoReserva.Chamada||r.Situacao==SituacaoReserva.EmAtendimento).ToListAsync(ct);
            if(ativas.Count(r=>Pertence(r,acesso))>=3) throw new RegraReservaException("Você já tem três solicitações em aberto. Cancele uma antes de reservar novamente.");
            var r=new Reserva {ChaveSolicitacao=dto.ChaveSolicitacao,Nome=dto.Nome.Trim(),Telefone=telefone,Pessoas=dto.Pessoas,
                UsuarioId=acesso.UsuarioId,VisitanteHash=acesso.UsuarioId.HasValue?null:acesso.VisitanteHash,CriadaEmUtc=horarios.Agora};
            if(fila)
            {
                horarios.ExigirFilaAberta();
                if(ativas.Any(x=>x.Telefone==telefone&&x.Tipo==TipoReserva.Fila)) throw new RegraReservaException("Este telefone já está na fila.");
                var instanteFila = horarios.Agora;
                if((horarios.FimAtendimentoAgora(instanteFila)-instanteFila).TotalMinutes<horarios.Regras.PermanenciaMinutos&&!dto.AceitaPermanenciaReduzida)
                    throw new RegraReservaException("Perto do fechamento, confirme que aceita encerrar o atendimento à meia-noite.");
                r.Tipo=TipoReserva.Fila;r.Situacao=SituacaoReserva.Aguardando;
                r.ExpiraEmUtc=HorariosReserva.Utc(horarios.Hoje,horarios.Regras.UltimaReservaMinutos).AddMinutes(1);
                Evento(r,"Entrada na fila presencial. Aguarde a chamada da recepção.");
            }
            else
            {
                if(!dto.Data.HasValue) throw new RegraReservaException("Escolha a data.");
                var(inicio,fim)=horarios.Periodo(dto.Data.Value,dto.Horario);
                if((fim-inicio).TotalMinutes<horarios.Regras.PermanenciaMinutos&&!dto.AceitaPermanenciaReduzida)
                    throw new RegraReservaException("Este horário tem menos de 90 minutos. Confirme que aceita encerrar à meia-noite.");
                if(ativas.Any(x=>x.Telefone==telefone&&x.InicioUtc<fim&&x.FimUtc>inicio))
                    throw new RegraReservaException("Este telefone já tem um atendimento/reserva nesse período.");
                var mesas=await db.MesasRestaurante.Where(m=>m.Ativa&&m.Capacidade>=dto.Pessoas).OrderBy(m=>m.Capacidade).ThenBy(m=>m.Id).ToListAsync(ct);
                var bloqueios=await Bloqueios(ct);
                var mesa=mesas.FirstOrDefault(m=>Livre(m.Id,inicio,fim,bloqueios));
                if(mesa==null) throw new RegraReservaException("Esse horário acabou de ficar sem mesas. Escolha outro horário ou entre na fila de hoje.");
                r.Tipo=TipoReserva.Agendada;r.Situacao=SituacaoReserva.Confirmada;r.MesaId=mesa.Id;
                r.InicioUtc=inicio;r.FimUtc=fim;r.ExpiraEmUtc=inicio.AddMinutes(horarios.Regras.ToleranciaMinutos);
                Evento(r,"Reserva confirmada. A mesa é atribuída pela recepção na chegada.");
            }
            db.Reservas.Add(r);await db.SaveChangesAsync(ct);
            return await Resposta(r,ct);
        },ct);
    }

    private static DateTime? Utc(DateTime? valor)=>valor.HasValue?DateTime.SpecifyKind(valor.Value,DateTimeKind.Utc):null;
    private async Task<ReservaResponseDTO> Resposta(Reserva r,CancellationToken ct)
    {
        int? posicao=null;
        if(r.Situacao==SituacaoReserva.Aguardando)
            posicao=await db.Reservas.CountAsync(x=>x.Situacao==SituacaoReserva.Aguardando&&x.ExpiraEmUtc>horarios.Agora
                &&(x.CriadaEmUtc<r.CriadaEmUtc||x.CriadaEmUtc==r.CriadaEmUtc&&x.Id<=r.Id),ct);
        var mesa=r.Situacao==SituacaoReserva.EmAtendimento
            ?await db.MesasRestaurante.Where(m=>m.Id==r.MesaId).Select(m=>m.Nome).SingleOrDefaultAsync(ct):null;
        return new(r.Codigo,r.Nome,r.Pessoas,r.Tipo,r.Situacao,HorariosReserva.Texto(r.Situacao),Utc(r.InicioUtc),Utc(r.FimUtc),DateTime.SpecifyKind(r.ExpiraEmUtc,DateTimeKind.Utc),
            posicao,mesa,r.Situacao is SituacaoReserva.Confirmada or SituacaoReserva.Aguardando or SituacaoReserva.Chamada,r.Versao);
    }
    public async Task<ReservaResponseDTO> Consultar(AcessoReserva acesso,Guid codigo,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        var r=await db.Reservas.AsNoTracking().SingleOrDefaultAsync(r=>r.Codigo==codigo,ct);
        if(r==null||!Pertence(r,acesso)) throw new KeyNotFoundException();
        return await Resposta(r,ct);
    }
    public async Task<List<ReservaResponseDTO>> Minhas(AcessoReserva acesso,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        var lista=await db.Reservas.AsNoTracking().Where(r=>(acesso.UsuarioId.HasValue&&r.UsuarioId==acesso.UsuarioId)
            ||(acesso.VisitanteHash!=null&&r.UsuarioId==null&&r.VisitanteHash==acesso.VisitanteHash))
            .OrderByDescending(r=>r.CriadaEmUtc).Take(50).ToListAsync(ct);
        var resultado=new List<ReservaResponseDTO>();foreach(var r in lista) resultado.Add(await Resposta(r,ct));return resultado;
    }
    public async Task Cancelar(AcessoReserva acesso,CancelarReservaDTO dto,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        await ComSala(async()=>
        {
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct);
            if(r==null||!Pertence(r,acesso)) throw new KeyNotFoundException();
            if(r.Situacao==SituacaoReserva.Cancelada) return true;
            if(r.Versao!=dto.Versao) throw new RegraReservaException("A reserva mudou. Atualize a página.");
            if(r.Situacao is not (SituacaoReserva.Confirmada or SituacaoReserva.Aguardando or SituacaoReserva.Chamada))
                throw new RegraReservaException("Esta solicitação não permite cancelamento pelo cliente.");
            r.Situacao=SituacaoReserva.Cancelada;Evento(r,"Cancelada pelo cliente; espaço liberado.");return true;
        },ct);
    }
    public async Task<ReservasAdminViewModel> Painel(int admin,DateOnly data,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        var inicio=HorariosReserva.Utc(data,0);var fim=inicio.AddDays(1);
        return new() {Data=data,Mesas=await db.MesasRestaurante.AsNoTracking().OrderBy(m=>m.Id).ToListAsync(ct),
            Reservas=await db.Reservas.AsNoTracking().Include(r=>r.Eventos).Where(r=>r.InicioUtc>=inicio&&r.InicioUtc<fim
                ||r.Tipo==TipoReserva.Fila&&r.CriadaEmUtc>=inicio&&r.CriadaEmUtc<fim||r.Situacao==SituacaoReserva.EmAtendimento)
                .OrderBy(r=>r.CriadaEmUtc).Take(300).ToListAsync(ct)};
    }
    public async Task Operar(int admin,OperarReservaDTO dto,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        await ComSala(async()=>
        {
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct)??throw new KeyNotFoundException();
            if(r.Versao!=dto.Versao) throw new RegraReservaException("A solicitação mudou. Atualize a página antes de operar.");
            switch(dto.Acao)
            {
                case "chegada":
                    if(r.Situacao is not (SituacaoReserva.Confirmada or SituacaoReserva.Chamada)) throw new RegraReservaException("Esta solicitação não está aguardando chegada.");
                    // Mesmo antecipando a chegada, o salão só abre às 11h.
                    if(horarios.Agora<HorariosReserva.Utc(horarios.Hoje,horarios.Regras.AberturaMinutos))
                        throw new RegraReservaException("Aguarde a abertura do restaurante para registrar a chegada.");
                    if(horarios.Agora<r.InicioUtc!.Value.AddMinutes(-15)) throw new RegraReservaException("Registre a chegada a partir de 15 minutos antes do horário.");
                    if(r.FimUtc<=horarios.Agora) throw new RegraReservaException("O atendimento já ultrapassou o horário de encerramento.");
                    var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==dto.MesaId&&m.Ativa&&m.Capacidade>=r.Pessoas,ct);
                    if(mesa==null||!Livre(mesa.Id,horarios.Agora,r.FimUtc!.Value,await Bloqueios(ct),r.Id))
                        throw new RegraReservaException("A mesa não está livre nesse período ou não comporta o grupo.");
                    r.MesaId=mesa.Id;r.ChegadaEmUtc=horarios.Agora;r.Situacao=SituacaoReserva.EmAtendimento;
                    Evento(r,$"Chegada registrada na {mesa.Nome}.",admin);break;
                case "encerrar":
                    if(r.Situacao!=SituacaoReserva.EmAtendimento) throw new RegraReservaException("O grupo ainda não está em atendimento.");
                    r.Situacao=SituacaoReserva.Concluida;r.EncerradaEmUtc=horarios.Agora;Evento(r,"Atendimento encerrado; mesa liberada pela recepção.",admin);break;
                case "cancelar":
                    if(!Aberta(r)||r.Situacao==SituacaoReserva.EmAtendimento) throw new RegraReservaException("Não é possível cancelar esta solicitação. Para mesas ocupadas, encerre o atendimento.");
                    if(string.IsNullOrWhiteSpace(dto.Motivo)||dto.Motivo.Length>400) throw new RegraReservaException("Informe o motivo do cancelamento (até 400 caracteres).");
                    r.Situacao=SituacaoReserva.Cancelada;Evento(r,"Cancelada pela administração: "+dto.Motivo.Trim(),admin);break;
                default:throw new RegraReservaException("Operação desconhecida.");
            }
            return true;
        },ct);
    }
    public async Task Chamar(int admin,int mesaId,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        await ComSala(async()=>
        {
            horarios.ExigirFilaAberta();var fim=horarios.FimAtendimentoAgora();
            var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==mesaId&&m.Ativa,ct);
            if(mesa==null||!Livre(mesaId,horarios.Agora,fim,await Bloqueios(ct)))
                throw new RegraReservaException("Mesa ocupada, inativa ou comprometida com uma reserva próxima.");
            var r=await db.Reservas.Where(r=>r.Situacao==SituacaoReserva.Aguardando&&r.Pessoas<=mesa.Capacidade)
                .OrderBy(r=>r.CriadaEmUtc).ThenBy(r=>r.Id).FirstOrDefaultAsync(ct)
                ??throw new RegraReservaException("Não há grupo aguardando que caiba nesta mesa.");
            r.Situacao=SituacaoReserva.Chamada;r.MesaId=mesaId;r.InicioUtc=horarios.Agora;r.FimUtc=fim;
            r.ExpiraEmUtc=new[]{horarios.Agora.AddMinutes(horarios.Regras.ChamadaMinutos),fim}.Min();
            Evento(r,$"Grupo chamado para a {mesa.Nome}. Prazo de chegada: {horarios.Regras.ChamadaMinutos} minutos.",admin);return true;
        },ct);
    }
    public async Task AlterarMesa(int admin,int mesaId,bool ativa,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        await ComSala(async()=>
        {
            var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==mesaId,ct)??throw new KeyNotFoundException();
            if(!ativa&&(await Bloqueios(ct)).Any(r=>r.MesaId==mesaId))
                throw new RegraReservaException("Há reservas ou atendimento nesta mesa. Resolva-os antes de desativar.");
            mesa.Ativa=ativa;return true;
        },ct);
    }
}
