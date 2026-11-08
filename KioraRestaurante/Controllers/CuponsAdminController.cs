using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Cupons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize(Roles = nameof(TipoUsuario.Administrador)), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CuponsAdminController(CupomService service) : Controller
{
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet] public async Task<IActionResult> Index()
    {
        try { return View("~/Views/Admin/Cupons/Index.cshtml", await service.Listar(AdminId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet] public async Task<IActionResult> Editar(int? id)
    {
        try
        {
            await service.ExigirAdministrador(AdminId);
            var agora = HorarioRestaurante.ParaLocal(DateTime.UtcNow);
            return View("~/Views/Admin/Cupons/Editar.cshtml", id.HasValue ? await service.BuscarEdicao(AdminId, id.Value)
                : new SalvarCupomDTO { InicioLocal = agora, FimLocal = agora.AddDays(14) });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost] public async Task<IActionResult> Editar(SalvarCupomDTO dados)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await service.Salvar(AdminId, dados);
                TempData["AdminSucesso"] = "Cupom salvo. A divulgação pode ser feita após conferir uma prévia da notificação.";
                return RedirectToAction(nameof(Relatorio), new { id });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Não foi possível salvar. Confira se o código já existe e abra a edição novamente."); }
        }
        return View("~/Views/Admin/Cupons/Editar.cshtml", dados);
    }
    [HttpGet] public async Task<IActionResult> Relatorio(int id, int pagina = 1)
    {
        try { return View("~/Views/Admin/Cupons/Relatorio.cshtml", await service.Relatorio(AdminId, id, pagina)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
