using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Services.Reservas;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ReservasController(ReservaService service,IdentidadeReserva identidade,HorariosReserva horarios):Controller
{
    private async Task Preparar(ReservaFormViewModel model,CancellationToken ct)
    {
        model.Hoje=horarios.Hoje;model.Limite=horarios.Hoje.AddDays(horarios.Regras.AntecedenciaDias);
        model.Dados.Data??=horarios.Hoje;
        try {model.Horarios=await service.Horarios(model.Dados.Data.Value,model.Dados.Pessoas,ct);}
        catch(RegraReservaException ex){ModelState.AddModelError("",ex.Message);}
    }
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? data,int pessoas=2,CancellationToken ct=default)
    {
        identidade.Obter(HttpContext,true);
        var model=new ReservaFormViewModel();model.Dados.Data=data;model.Dados.Pessoas=pessoas;
        model.Dados.Nome=User.Identity?.IsAuthenticated==true?User.Identity.Name??"":"";
        await Preparar(model,ct);return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Criar(ReservaFormViewModel model,string tipo,CancellationToken ct)
    {
        if(tipo is not ("reserva" or "fila")) return BadRequest();
        if(ModelState.IsValid)
        {
            try {
                var resultado=await service.Criar(identidade.Obter(HttpContext),model.Dados,tipo=="fila",ct);
                return RedirectToAction(nameof(Detalhes),new{id=resultado.Codigo});
            }
            catch(UnauthorizedAccessException){return Forbid();}
            catch(KeyNotFoundException){return NotFound();}
            catch(Exception ex) when(ex is RegraReservaException or ValidationException){ModelState.AddModelError("",ex.Message);}
            catch(DbUpdateException){ModelState.AddModelError("","Não foi possível confirmar a solicitação. Consulte Minhas reservas antes de repetir.");}
        }
        await Preparar(model,ct);return View("Index",model);
    }
    [HttpGet]
    public async Task<IActionResult> Horarios(DateOnly data,int pessoas,CancellationToken ct)
    {
        if(!ModelState.IsValid)return BadRequest();
        try {return Json(await service.Horarios(data,pessoas,ct));}
        catch(RegraReservaException ex){return BadRequest(new{mensagem=ex.Message});}
    }
    [HttpGet]
    public async Task<IActionResult> Detalhes(Guid id,CancellationToken ct)
    {
        try{return View(await service.Consultar(identidade.Obter(HttpContext),id,ct));}
        catch(KeyNotFoundException){return NotFound();}catch(UnauthorizedAccessException){return NotFound();}
    }
    [HttpGet]
    public async Task<IActionResult> Consultar(Guid id,CancellationToken ct)
    {
        try{return Json(await service.Consultar(identidade.Obter(HttpContext),id,ct));}
        catch(KeyNotFoundException){return NotFound();}catch(UnauthorizedAccessException){return NotFound();}
    }
    [HttpGet]
    public async Task<IActionResult> Minhas(CancellationToken ct)
    {
        try{return View(await service.Minhas(identidade.Obter(HttpContext,true),ct));}
        catch(UnauthorizedAccessException){return Forbid();}
    }
    [HttpPost]
    public async Task<IActionResult> Cancelar(CancelarReservaDTO dto,CancellationToken ct)
    {
        try{await service.Cancelar(identidade.Obter(HttpContext),dto,ct);TempData["ReservaAviso"]="Solicitação cancelada. O espaço foi liberado.";}
        catch(KeyNotFoundException){return NotFound();}catch(UnauthorizedAccessException){return Forbid();}
        catch(RegraReservaException ex){TempData["ReservaErro"]=ex.Message;}
        catch(DbUpdateException){TempData["ReservaErro"]="Não foi possível confirmar o cancelamento. Atualize a página.";}
        return RedirectToAction(nameof(Detalhes),new{id=dto.Codigo});
    }
}
