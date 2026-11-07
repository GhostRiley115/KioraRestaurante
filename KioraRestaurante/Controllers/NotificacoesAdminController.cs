using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Notificacoes;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Cupons;
using KioraRestaurante.Services.Notificacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize(Roles = nameof(TipoUsuario.Administrador)), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificacoesAdminController(NotificacaoService service, CupomService cupons) : Controller
{
    private int AdminId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task PrepararOpcoes() => ViewData["Cupons"] = (await cupons.Listar(AdminId)).Where(c => c.Ativo && c.FimEmUtc > DateTime.UtcNow).ToList();
    [HttpGet] public async Task<IActionResult> Index()
    {
        try { return View("~/Views/Admin/Notificacoes/Index.cshtml", await service.Comunicados(AdminId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet] public async Task<IActionResult> Criar(int? cupomId)
    {
        try
        {
            await PrepararOpcoes(); var dto = new CriarNotificacaoDTO { CupomId = cupomId };
            if (cupomId.HasValue)
            {
                var c = await cupons.BuscarEdicao(AdminId, cupomId.Value);
                dto.Titulo = "Novo cupom na Kiora";
                dto.Mensagem = $"Use o cupom {c.Codigo} no checkout. Consulte os detalhes para conferir desconto, validade e condições.";
            }
            return View("~/Views/Admin/Notificacoes/Criar.cshtml", dto);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost] public async Task<IActionResult> Previa(CriarNotificacaoDTO dados)
    {
        try
        {
            if (ModelState.IsValid) return View("~/Views/Admin/Notificacoes/Previa.cshtml", await service.Preparar(AdminId, dados));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
        try { await PrepararOpcoes(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return View("~/Views/Admin/Notificacoes/Criar.cshtml", dados);
    }
    [HttpPost] public async Task<IActionResult> Publicar(CriarNotificacaoDTO dados)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await service.Publicar(AdminId, dados);
                TempData["AdminSucesso"] = "Notificação publicada. Ela está disponível nas contas ativas do público selecionado.";
                return RedirectToAction(nameof(Index));
            }
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Não foi possível confirmar a publicação. Consulte o histórico antes de tentar novamente."); }
        try { await PrepararOpcoes(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return View("~/Views/Admin/Notificacoes/Criar.cshtml", dados);
    }
}
