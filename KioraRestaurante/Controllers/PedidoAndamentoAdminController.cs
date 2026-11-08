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
    [HttpPost,Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Cancelar(KioraRestaurante.DTOs.Admin.CancelarPedidoAdminRequestDTO dto,[FromServices] KioraRestaurante.Services.GerenciarPedidoService cancelamentos,CancellationToken ct)
    {
        if(!ModelState.IsValid)TempData["AdminErro"]="Informe um motivo válido, com até 300 caracteres, e atualize os detalhes.";
        else try
        {
            var adminId=int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
            var resultado=await cancelamentos.CancelarPeloAdministrador(adminId,dto,ct);
            if(!resultado.Concluido)TempData["AdminErro"]="O cancelamento está em conferência. O pedido ainda não foi cancelado; atualize os detalhes para acompanhar.";
            else TempData["AdminSucesso"]=resultado.ReembolsoPendente?"Pedido cancelado. Confira a devolução do valor recebido, incluindo ajustes de entrega. Nenhum estorno automático foi confirmado.":"Pedido cancelado. O cliente receberá uma notificação no site.";
        }
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(ValidationException ex){TempData["AdminErro"]=ex.Message;}
        catch(DbUpdateConcurrencyException){TempData["AdminErro"]="O pedido mudou durante o cancelamento. Confira a situação atual.";}
        catch(DbUpdateException){TempData["AdminErro"]="Não foi possível confirmar o cancelamento. Atualize os detalhes antes de repetir.";}
        return RedirectToAction("DetalhesPedido","Admin",new{id=dto.PedidoId});
    }

}
