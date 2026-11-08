using System.Security.Claims;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.Services.Notificacoes;
using KioraRestaurante.DTOs.Salao;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace KioraRestaurante.Controllers;

[ApiController,Route("api/equipe"),AutoValidateAntiforgeryToken,EnableRateLimiting("salao")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class EquipeApiController(IAntiforgery antiforgery,AcessoSalaoService acesso,NotificacaoService avisos):ControllerBase
{
    private int Id=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    [HttpGet("sessao")] public async Task<IActionResult> Sessao(CancellationToken ct)
    {
        object? conta=null;
        if(User.Identity?.IsAuthenticated==true)
        {
            try {var u=await acesso.Exigir(Id,ct,TipoUsuario.Garcom,TipoUsuario.Gerente,TipoUsuario.Portaria,TipoUsuario.Cozinha);
                conta=new{id=u.Id,nome=u.Nome,cargo=u.Tipo.ToString(),gestor=AcessoSalaoService.Gestor(u.Tipo)};}
            catch(UnauthorizedAccessException){return Forbid();}
        }
        return Ok(new{csrfToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken,usuario=conta,fuso="America/Sao_Paulo"});
    }
    [Authorize,HttpPost("logout")] public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);return Ok(new{sucesso=true});}
    [Authorize,HttpGet("notificacoes")] public async Task<IActionResult> Notificacoes(int pagina=1,CancellationToken ct=default)
    {try {return Ok(await avisos.Listar(Id,pagina,ct));}catch(UnauthorizedAccessException){return Forbid();}}
    [Authorize,HttpGet("notificacoes/{id:guid}")] public async Task<IActionResult> Detalhe(Guid id,CancellationToken ct)
    {try{return Ok(await avisos.Detalhe(Id,id,ct));}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}}
    [Authorize,HttpPost("notificacoes/ler")] public async Task<IActionResult> Ler(LeituraNotificacaoDTO dto,CancellationToken ct)
    {try{await avisos.MarcarLida(Id,dto.Id,ct);return Ok(new{sucesso=true});}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}}
}
public class LeituraNotificacaoDTO { public Guid Id { get; set; } }
