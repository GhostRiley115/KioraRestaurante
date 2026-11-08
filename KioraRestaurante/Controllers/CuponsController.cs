using System.Security.Claims;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CuponsController(AppDbContext db) : Controller
{
    [HttpGet] public async Task<IActionResult> Detalhes(int id)
    {
        var usuarioId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var valor) ? valor : 0;
        var tipo = await db.Usuarios.Where(u => u.Id == usuarioId && u.Ativo).Select(u => (TipoUsuario?)u.Tipo).SingleOrDefaultAsync();
        if (tipo == null) return Forbid();
        if (tipo != TipoUsuario.Administrador && !await db.NotificacoesUsuarios.AnyAsync(n => n.UsuarioId == usuarioId
                && n.Notificacao.CupomId == id && n.Notificacao.Publico != KioraRestaurante.Models.Notificacoes.PublicoNotificacao.Administrador)) return NotFound();
        var cupom = await db.Cupons.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id);
        return cupom == null ? NotFound() : View(cupom);
    }
}
