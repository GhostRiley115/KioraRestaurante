using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Pedidos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize(Roles = nameof(TipoUsuario.Administrador)), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PedidoAndamentoAdminController(AndamentoPedidoService service) : Controller
{
    [HttpPost] public async Task<IActionResult> Avancar(int id, Guid versao, StatusPedido proximo, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["AdminErro"] = "Solicitação inválida. Atualize os detalhes."; return RedirectToAction("DetalhesPedido", "Admin", new { id }); }
        try
        {
            var adminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var valor) ? valor : 0;
            await service.Avancar(adminId, id, versao, proximo, ct);
            TempData["AdminSucesso"] = "Etapa atualizada. O cliente receberá uma notificação no site.";
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { TempData["AdminErro"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["AdminErro"] = "O pedido mudou durante a atualização. Confira a etapa atual."; }
        catch (DbUpdateException) { TempData["AdminErro"] = "Não foi possível confirmar a alteração. Atualize os detalhes."; }
        return RedirectToAction("DetalhesPedido", "Admin", new { id });
    }
}
