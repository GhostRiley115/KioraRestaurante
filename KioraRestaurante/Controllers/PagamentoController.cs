using System.Security.Claims;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;
[Authorize, AutoValidateAntiforgeryToken]
public class PagamentoController(CheckoutOnlineService pagamentos, AppDbContext db) : Controller
{
    private int UsuarioId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken ct)
    {
        try
        {
            var pedido = await pagamentos.ExigirPedido(UsuarioId, id, ct);
            if (pedido.FormaPagamento != FormaPagamento.MercadoPago) return NotFound();
            return View(pedido);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost]
    public async Task<IActionResult> Pagar(int id, CancellationToken ct)
    {
        try { return Redirect(await pagamentos.Iniciar(UsuarioId, id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is FalhaPagamento or DbUpdateException)
        {
            TempData["PagamentoAviso"] = ex is FalhaPagamento ? ex.Message : "Consulte novamente este pedido antes de tentar pagar.";
            return RedirectToAction(nameof(Index), new { id });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Consultar(int id, CancellationToken ct)
    {
        try
        {
            await pagamentos.ExigirPedido(UsuarioId, id, ct);
            var orderId = await db.PagamentosOnline.AsNoTracking().Where(p => p.PedidoId == id)
                .Select(p => p.OrderId).SingleOrDefaultAsync(ct);
            if (orderId != null) await pagamentos.Sincronizar(orderId, ct);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is FalhaPagamento or DbUpdateException)
        { TempData["PagamentoAviso"] = "Não foi possível atualizar agora. Tente consultar novamente."; }
        return RedirectToAction(nameof(Index), new { id });
    }
    [HttpGet]
    public IActionResult Retorno(int id) => RedirectToAction(nameof(Index), new { id });
}
