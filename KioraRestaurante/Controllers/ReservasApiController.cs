using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

// Mesmas regras do MVC. Enquanto a autenticação é por cookie, o app deve manter cookies
// e enviar X-CSRF-TOKEN obtido em sessao nas operações de escrita.
[ApiController,Route("api/reservas"),AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ReservasApiController(ReservaService service,IdentidadeReserva identidade,IAntiforgery antiforgery,HorariosReserva horarios):ControllerBase
{
    private int Admin=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<IActionResult> Executar(Func<Task<object?>> acao)
    {
        try{return Ok(await acao());}
        catch(UnauthorizedAccessException){return Unauthorized();}catch(KeyNotFoundException){return NotFound();}
        catch(Exception ex) when(ex is RegraReservaException or ValidationException){return Conflict(new{mensagem=ex.Message});}
        catch(DbUpdateException){return StatusCode(409,new{mensagem="A operação não pôde ser confirmada. Consulte a solicitação antes de repetir."});}
    }
    [HttpGet("sessao")]
    public IActionResult Sessao()=>Ok(new {chaveVisitante=identidade.Chave(HttpContext,true),
        csrfToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken,hoje=horarios.Hoje,
        ultimaData=horarios.Hoje.AddDays(horarios.Regras.AntecedenciaDias),fuso="America/Sao_Paulo"});
    [HttpGet("horarios")]
    public Task<IActionResult> Horarios(DateOnly data,int pessoas,CancellationToken ct)=>Executar(async()=>await service.Horarios(data,pessoas,ct));
    [HttpGet("minhas")]
    public Task<IActionResult> Minhas(CancellationToken ct)=>Executar(async()=>await service.Minhas(identidade.Obter(HttpContext),ct));
    [HttpGet("{codigo:guid}")]
    public Task<IActionResult> Consultar(Guid codigo,CancellationToken ct)=>Executar(async()=>await service.Consultar(identidade.Obter(HttpContext),codigo,ct));
    [HttpPost]
    public Task<IActionResult> Criar(CriarReservaDTO dto,CancellationToken ct)=>Executar(async()=>await service.Criar(identidade.Obter(HttpContext),dto,false,ct));
    [HttpPost("fila")]
    public Task<IActionResult> Fila(CriarReservaDTO dto,CancellationToken ct)=>Executar(async()=>await service.Criar(identidade.Obter(HttpContext),dto,true,ct));
    [HttpPost("cancelar")]
    public Task<IActionResult> Cancelar(CancelarReservaDTO dto,CancellationToken ct)=>Executar(async()=>{await service.Cancelar(identidade.Obter(HttpContext),dto,ct);return new{sucesso=true};});
    [Authorize(Roles=nameof(TipoUsuario.Administrador)),HttpGet("admin")]
    public Task<IActionResult> Painel(DateOnly? data,CancellationToken ct)=>Executar(async()=>
    {
        var painel=await service.Painel(Admin,data??horarios.Hoje,ct);
        // Projeta apenas o contrato público; não serializa navegações cíclicas ou hashes.
        return new {painel.Data,painel.Mesas,Reservas=painel.Reservas.Select(r=>new {
            r.Codigo,r.Nome,r.Telefone,r.Pessoas,r.Tipo,r.Situacao,r.MesaId,
            InicioUtc=r.InicioUtc.HasValue?DateTime.SpecifyKind(r.InicioUtc.Value,DateTimeKind.Utc):(DateTime?)null,
            FimUtc=r.FimUtc.HasValue?DateTime.SpecifyKind(r.FimUtc.Value,DateTimeKind.Utc):(DateTime?)null,
            ExpiraEmUtc=DateTime.SpecifyKind(r.ExpiraEmUtc,DateTimeKind.Utc),r.Versao,
            Eventos=r.Eventos.Select(e=>new{e.DataUtc,e.Descricao,e.AdministradorId})})};
    });
    [Authorize(Roles=nameof(TipoUsuario.Administrador)),HttpPost("admin/operar")]
    public Task<IActionResult> Operar(OperarReservaDTO dto,CancellationToken ct)=>Executar(async()=>{await service.Operar(Admin,dto,ct);return new{sucesso=true};});
    [Authorize(Roles=nameof(TipoUsuario.Administrador)),HttpPost("admin/chamar/{mesaId:int}")]
    public Task<IActionResult> Chamar(int mesaId,CancellationToken ct)=>Executar(async()=>{await service.Chamar(Admin,mesaId,ct);return new{sucesso=true};});
}
