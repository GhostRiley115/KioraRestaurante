using System.Security.Claims;
using KioraRestaurante.Services.Notificacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers;

[Authorize, AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificacoesController(NotificacaoService service) : Controller
{
    private int UsuarioId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<IActionResult> Consultar(Func<Task<IActionResult>> acao)
    {
        try { return await acao(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpGet] public Task<IActionResult> Index(int pagina = 1) => Consultar(async () => View(await service.Listar(UsuarioId, pagina)));
    [HttpGet] public Task<IActionResult> Resumo() => Consultar(async () => Ok(new { naoLidas = await service.NaoLidas(UsuarioId) }));
    [HttpGet] public Task<IActionResult> Painel() => Consultar(async () => {
        var lista = await service.Listar(UsuarioId, 1);
        return Ok(new { lista.NaoLidas, itens = lista.Itens.Take(10).Select(n => new { n.Id, n.Titulo, n.NaoLida }) });
    });
    [HttpGet] public Task<IActionResult> Detalhes(Guid id) => Consultar(async () => View(await service.Detalhe(UsuarioId, id)));
    [HttpPost] public Task<IActionResult> Ler(Guid id) => Consultar(async () => {
        await service.MarcarLida(UsuarioId, id); return RedirectToAction(nameof(Detalhes), new { id });
    });
    [HttpPost] public Task<IActionResult> MarcarLida(Guid id) => Consultar(async () => { await service.MarcarLida(UsuarioId, id); return RedirectToAction(nameof(Index)); });
    [HttpPost] public Task<IActionResult> MarcarTodas() => Consultar(async () => { await service.MarcarTodas(UsuarioId); return RedirectToAction(nameof(Index)); });
    [HttpPost] public Task<IActionResult> Abrir(Guid id) => Consultar(async () => {
        var destino = await service.Abrir(UsuarioId, id);
        return destino == null ? RedirectToAction(nameof(Index)) : RedirectToAction(destino.Action, destino.Controller,
            new { id = (object?)destino.Id ?? destino.Codigo, data = destino.Data });
    });
}
