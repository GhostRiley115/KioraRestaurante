using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Reservas;

public class ReservaService(AppDbContext db,HorariosReserva horarios)
{
    private static bool Pertence(Reserva r,AcessoReserva a)=>(a.UsuarioId.HasValue&&r.UsuarioId==a.UsuarioId)
        ||(r.UsuarioId==null&&a.VisitanteHash!=null&&r.VisitanteHash==a.VisitanteHash);
    private async Task ValidarAcesso(AcessoReserva a,CancellationToken ct)
    {
        if(a.UsuarioId.HasValue){if(!await db.Usuarios.AnyAsync(u=>u.Id==a.UsuarioId&&u.Ativo,ct))throw new UnauthorizedAccessException();}
        else if(a.VisitanteHash==null)throw new UnauthorizedAccessException();
    }
    private async Task ExigirAdmin(int id,CancellationToken ct)
    {if(!await db.Usuarios.AnyAsync(u=>u.Id==id&&u.Ativo&&(u.Tipo==TipoUsuario.Administrador||u.Tipo==TipoUsuario.Gerente),ct))throw new UnauthorizedAccessException();}
    private void Evento(Reserva r,string texto,int? administrador=null)
    {r.Versao=Guid.NewGuid();r.Eventos.Add(new(){DataUtc=horarios.Agora,Descricao=texto,AdministradorId=administrador});}
    private async Task Expirar(CancellationToken ct)
    {
        var agora=horarios.Agora;
        var atrasadas=await db.Reservas.Where(r=>(r.Situacao==SituacaoReserva.AguardandoChegada||r.Situacao==SituacaoReserva.Confirmada
            ||r.Situacao==SituacaoReserva.Aguardando||r.Situacao==SituacaoReserva.Chamada)&&r.ExpiraEmUtc<=agora).ToListAsync(ct);
        foreach(var r in atrasadas)
        {
            var fim=HorariosReserva.Utc(horarios.Hoje,horarios.Regras.FechamentoMinutos);
            if(r.Situacao==SituacaoReserva.Chamada&&r.ChegadaEmUtc!=null&&agora<fim)
            {r.Situacao=SituacaoReserva.Aguardando;r.MesaId=null;r.ExpiraEmUtc=fim;Evento(r,"Chamada encerrada; grupo continua na fila para nova acomodação.");}
            else {r.Situacao=SituacaoReserva.Expirada;Evento(r,"Prazo de chegada/espera encerrado automaticamente.");}
        }
        // Uma mesa ocupada nunca é liberada automaticamente pelo relógio.
    }
    private async Task<T> ComSala<T>(Func<Task<T>> acao,CancellationToken ct)
    {
        await using var transaction=await TransacaoSalao.Iniciar(db,ct);
        try
        {
            await Expirar(ct);await db.SaveChangesAsync(ct);var resultado=await acao();
            await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return resultado;
        }
        catch {await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();throw;}
    }
    public Task Manutencao(CancellationToken ct)=>ComSala(()=>Task.FromResult(true),ct);
    public async Task<List<HorarioReservaDTO>> Horarios(DateOnly data,int pessoas,CancellationToken ct)
    {
        horarios.ValidarData(data);if(pessoas is <1 or >6)throw new RegraReservaException("Escolha de 1 a 6 pessoas.");
        var comporta=await db.MesasRestaurante.AnyAsync(m=>m.Ativa&&m.Capacidade>=pessoas,ct);
        var lista=new List<HorarioReservaDTO>();
        for(var minuto=horarios.Regras.AberturaMinutos;minuto<=horarios.Regras.UltimaReservaMinutos;minuto+=horarios.Regras.IntervaloMinutos)
            if(HorariosReserva.Utc(data,minuto)>=horarios.Agora)lista.Add(new(HorariosReserva.Hora(minuto),"",0,comporta));
        return lista;
    }
    public async Task<ReservaResponseDTO> Criar(AcessoReserva acesso,CriarReservaDTO dto,bool fila,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);Validator.ValidateObject(dto,new(dto),true);
        if(dto.ChaveSolicitacao==Guid.Empty)throw new RegraReservaException("Atualize o formulário antes de enviar.");
        var telefone=new string(dto.Telefone.Where(char.IsAsciiDigit).ToArray());
        if(telefone.Length is not (10 or 11))throw new RegraReservaException("Informe o telefone com DDD (10 ou 11 números).");
        return await ComSala(async()=>
        {
            var anterior=await db.Reservas.SingleOrDefaultAsync(r=>r.ChaveSolicitacao==dto.ChaveSolicitacao,ct);
            if(anterior!=null){if(!Pertence(anterior,acesso))throw new KeyNotFoundException();return await Resposta(anterior,ct);}
            var ativas=await db.Reservas.Where(r=>r.Situacao==SituacaoReserva.AguardandoChegada||r.Situacao==SituacaoReserva.Confirmada||r.Situacao==SituacaoReserva.Aguardando
                ||r.Situacao==SituacaoReserva.Chamada||r.Situacao==SituacaoReserva.EmAtendimento).ToListAsync(ct);
            if(ativas.Count(r=>Pertence(r,acesso))>=3)throw new RegraReservaException("Você já tem três solicitações em aberto. Cancele uma antes de reservar novamente.");
            if(!await db.MesasRestaurante.AnyAsync(m=>m.Ativa&&m.Capacidade>=dto.Pessoas,ct))throw new RegraReservaException("Não há mesa habilitada que comporte esse grupo.");
            var r=new Reserva{ChaveSolicitacao=dto.ChaveSolicitacao,Nome=dto.Nome.Trim(),Telefone=telefone,Pessoas=dto.Pessoas,
                UsuarioId=acesso.UsuarioId,VisitanteHash=acesso.UsuarioId.HasValue?null:acesso.VisitanteHash,CriadaEmUtc=horarios.Agora,Situacao=SituacaoReserva.AguardandoChegada};
            if(fila)
            {
                horarios.ExigirFilaAberta();
                if(ativas.Any(x=>x.Telefone==telefone&&x.Tipo==TipoReserva.Fila))throw new RegraReservaException("Este telefone já tem uma solicitação na fila.");
                r.Tipo=TipoReserva.Fila;r.ExpiraEmUtc=HorariosReserva.Utc(horarios.Hoje,horarios.Regras.FechamentoMinutos);
                Evento(r,"Solicitação recebida. Apresente-se à portaria para registrar a chegada e entrar na fila presencial.");
            }
            else
            {
                if(!dto.Data.HasValue)throw new RegraReservaException("Escolha a data.");
                var(inicio,_)=horarios.Periodo(dto.Data.Value,dto.Horario);
                if(ativas.Any(x=>x.Telefone==telefone&&x.InicioUtc==inicio))throw new RegraReservaException("Este telefone já possui uma solicitação nesse horário.");
                r.Tipo=TipoReserva.Agendada;r.InicioUtc=inicio;r.ExpiraEmUtc=inicio.AddMinutes(horarios.Regras.ToleranciaMinutos);
                Evento(r,"Reserva recebida, aguardando chegada. A acomodação depende de mesa disponível e é confirmada pelo garçom.");
            }
            db.Reservas.Add(r);await db.SaveChangesAsync(ct);return await Resposta(r,ct);
        },ct);
    }
    private static DateTime? Utc(DateTime? valor)=>valor.HasValue?DateTime.SpecifyKind(valor.Value,DateTimeKind.Utc):null;
    private async Task<ReservaResponseDTO> Resposta(Reserva r,CancellationToken ct)
    {
        int? posicao=null;
        if(r.Situacao==SituacaoReserva.Aguardando&&r.ChegadaEmUtc.HasValue)
            posicao=await db.Reservas.CountAsync(x=>x.Situacao==SituacaoReserva.Aguardando&&x.ChegadaEmUtc!=null&&x.ExpiraEmUtc>horarios.Agora
                &&(x.ChegadaEmUtc<r.ChegadaEmUtc||x.ChegadaEmUtc==r.ChegadaEmUtc&&x.Id<=r.Id),ct);
        var mesa=r.Situacao==SituacaoReserva.EmAtendimento?await db.MesasRestaurante.Where(m=>m.Id==r.MesaId).Select(m=>m.Nome).SingleOrDefaultAsync(ct):null;
        return new(r.Codigo,r.Nome,r.Pessoas,r.Tipo,r.Situacao,HorariosReserva.Texto(r.Situacao),Utc(r.InicioUtc),null,DateTime.SpecifyKind(r.ExpiraEmUtc,DateTimeKind.Utc),
            posicao,mesa,r.Situacao is SituacaoReserva.AguardandoChegada or SituacaoReserva.Confirmada or SituacaoReserva.Aguardando or SituacaoReserva.Chamada,r.Versao);
    }
    public async Task<ReservaResponseDTO> Consultar(AcessoReserva acesso,Guid codigo,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);var r=await db.Reservas.AsNoTracking().SingleOrDefaultAsync(r=>r.Codigo==codigo,ct);
        if(r==null||!Pertence(r,acesso))throw new KeyNotFoundException();return await Resposta(r,ct);
    }
    public async Task<List<ReservaResponseDTO>> Minhas(AcessoReserva acesso,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        var lista=await db.Reservas.AsNoTracking().Where(r=>(acesso.UsuarioId.HasValue&&r.UsuarioId==acesso.UsuarioId)
            ||(acesso.VisitanteHash!=null&&r.UsuarioId==null&&r.VisitanteHash==acesso.VisitanteHash)).OrderByDescending(r=>r.CriadaEmUtc).Take(50).ToListAsync(ct);
        var resultado=new List<ReservaResponseDTO>();foreach(var r in lista)resultado.Add(await Resposta(r,ct));return resultado;
    }
    public async Task Cancelar(AcessoReserva acesso,CancelarReservaDTO dto,CancellationToken ct)
    {
        await ValidarAcesso(acesso,ct);
        await ComSala(async()=>
        {
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct);
            if(r==null||!Pertence(r,acesso))throw new KeyNotFoundException();if(r.Situacao==SituacaoReserva.Cancelada)return true;
            if(r.Versao!=dto.Versao)throw new RegraReservaException("A reserva mudou. Atualize a página.");
            if(r.Situacao is not (SituacaoReserva.AguardandoChegada or SituacaoReserva.Confirmada or SituacaoReserva.Aguardando or SituacaoReserva.Chamada))throw new RegraReservaException("Esta solicitação não permite cancelamento pelo cliente.");
            r.Situacao=SituacaoReserva.Cancelada;Evento(r,"Cancelada pelo cliente; espaço liberado.");return true;
        },ct);
    }
    public async Task<ReservasAdminViewModel> Painel(int admin,DateOnly data,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);var inicio=HorariosReserva.Utc(data,0);var fim=inicio.AddDays(1);
        return new(){Data=data,Mesas=await db.MesasRestaurante.AsNoTracking().OrderBy(m=>m.Id).ToListAsync(ct),Reservas=await db.Reservas.AsNoTracking().Include(r=>r.Eventos)
            .Where(r=>r.InicioUtc>=inicio&&r.InicioUtc<fim||r.Tipo==TipoReserva.Fila&&r.CriadaEmUtc>=inicio&&r.CriadaEmUtc<fim||r.Situacao==SituacaoReserva.EmAtendimento).OrderBy(r=>r.CriadaEmUtc).Take(300).ToListAsync(ct)};
    }
    public async Task Operar(int admin,OperarReservaDTO dto,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        if(dto.Acao!="cancelar")throw new RegraReservaException("Use o painel do salão para acomodar o grupo, fechar a comanda e liberar a mesa.");
        if(string.IsNullOrWhiteSpace(dto.Motivo)||dto.Motivo.Length>400)throw new RegraReservaException("Informe o motivo do cancelamento (até 400 caracteres).");
        await ComSala(async()=>
        {
            var r=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.Codigo,ct)??throw new KeyNotFoundException();
            if(r.Versao!=dto.Versao)throw new RegraReservaException("A solicitação mudou. Atualize a página antes de operar.");
            if(r.Situacao is SituacaoReserva.EmAtendimento or SituacaoReserva.Concluida or SituacaoReserva.Expirada or SituacaoReserva.Cancelada)throw new RegraReservaException("Esta solicitação não pode ser cancelada neste estado.");
            r.Situacao=SituacaoReserva.Cancelada;Evento(r,"Cancelada pela administração: "+dto.Motivo.Trim(),admin);return true;
        },ct);
    }
    public async Task Chamar(int admin,int mesaId,CancellationToken ct)
    {await ExigirAdmin(admin,ct);throw new RegraReservaException("Use o painel da portaria ou POST /api/salao/fila/chamar, com uma chave de solicitação.");}
    public async Task AlterarMesa(int admin,int mesaId,bool ativa,CancellationToken ct)
    {
        await ExigirAdmin(admin,ct);
        await ComSala(async()=>
        {
            var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==mesaId,ct)??throw new KeyNotFoundException();
            if(await db.AtendimentosMesa.AnyAsync(a=>a.MesaOcupadaId==mesaId,ct)||await db.Reservas.AnyAsync(r=>r.MesaId==mesaId&&r.Situacao==SituacaoReserva.Chamada&&r.ExpiraEmUtc>horarios.Agora,ct))throw new RegraReservaException("Libere o atendimento ou a chamada antes de configurar a mesa.");
            mesa.Ativa=ativa;mesa.Versao=Guid.NewGuid();return true;
        },ct);
    }
}

// Comentários históricos preservados; o fluxo atual não utiliza permanência de 90 minutos.
// Mesmo antecipando a chegada, o salão só abre às 11h.
