using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Services.Salao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;
[ApiController,Route("api/equipe/funcionarios"),Authorize(Roles="Administrador,Gerente"),AutoValidateAntiforgeryToken,EnableRateLimiting("salao")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class FuncionariosApiController(FuncionarioService service):ControllerBase
{
    private int Id=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<IActionResult> Executar(Func<Task<object?>> acao)
    {
        try{return Ok(await acao());}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(ValidationException ex){return Conflict(new{mensagem=ex.Message});}catch(DbUpdateException){return Conflict(new{mensagem="O cadastro mudou ou o e-mail já está em uso. Atualize a consulta."});}
    }
    [HttpGet] public Task<IActionResult> Listar(CancellationToken ct)=>Executar(async()=>await service.Listar(Id,ct));
    [HttpGet("{id:int}")] public Task<IActionResult> Edicao(int id,CancellationToken ct)=>Executar(async()=>await service.Edicao(Id,id,ct));
    [HttpPost] public Task<IActionResult> Salvar(SalvarFuncionarioDTO dto,CancellationToken ct)=>Executar(async()=>await service.Salvar(Id,dto,ct));
    [HttpPost("reenviar-convite"),EnableRateLimiting("convite-funcionario")] public Task<IActionResult> Reenviar(ReenviarConviteDTO dto,CancellationToken ct)=>Executar(async()=>new{enviado=await service.Reenviar(Id,dto.FuncionarioId,dto.Versao,ct)});
}
public class ReenviarConviteDTO {public int FuncionarioId{get;set;}public Guid Versao{get;set;}}
