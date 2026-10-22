using System.Security.Claims;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

[Authorize(Roles=nameof(TipoUsuario.Administrador))]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ReservasAdminController(ReservaService service,HorariosReserva horarios):Controller
{
    private int Admin=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? data,CancellationToken ct)
    {
        if(!ModelState.IsValid)return BadRequest();
        try{return View("~/Views/Admin/Reservas/Index.cshtml",await service.Painel(Admin,data??horarios.Hoje,ct));}
        catch(UnauthorizedAccessException){return Forbid();}
    }
    private async Task<IActionResult> Executar(Func<Task> acao)
    {
        if(!ModelState.IsValid)return BadRequest();
        try{await acao();TempData["AdminSucesso"]="Operação registrada.";}
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(RegraReservaException ex){TempData["AdminErro"]=ex.Message;}
        catch(DbUpdateException){TempData["AdminErro"]="A operação não pôde ser confirmada. Atualize a página antes de repetir.";}
        return RedirectToAction(nameof(Index));
    }
    [HttpPost] public Task<IActionResult> Operar(OperarReservaDTO dto,CancellationToken ct)=>Executar(()=>service.Operar(Admin,dto,ct));
    [HttpPost] public Task<IActionResult> Chamar(int mesaId,CancellationToken ct)=>Executar(()=>service.Chamar(Admin,mesaId,ct));
    [HttpPost] public Task<IActionResult> Mesa(int mesaId,bool ativa,CancellationToken ct)=>Executar(()=>service.AlterarMesa(Admin,mesaId,ativa,ct));
}
