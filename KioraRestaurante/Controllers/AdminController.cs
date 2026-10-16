using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

// A proteção vale também para acesso direto por URL e envio manual de requisições.
[Authorize(Roles = nameof(TipoUsuario.Administrador))]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminController : Controller
{
    private readonly IAdminService _service;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService service, ILogger<AdminController> logger) { _service = service; _logger = logger; }
    private int AdministradorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Centraliza a resposta de acesso negado sem expor erros internos.
    private async Task<IActionResult> Consultar(Func<Task<IActionResult>> acao)
    {
        try { return await acao(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public Task<IActionResult> Index() => Consultar(async () => View(await _service.Painel(AdministradorId)));

    [HttpGet]
    public Task<IActionResult> Usuarios([FromQuery] FiltroUsuariosAdmin filtros) => Consultar(async () => {
        var model = new UsuariosAdminViewModel { Filtros = filtros };
        if (ModelState.IsValid) model.Resultado = await _service.Usuarios(AdministradorId, filtros);
        return View("~/Views/Admin/Usuarios/Index.cshtml", model);
    });

    [HttpGet]
    public Task<IActionResult> Pedidos([FromQuery] FiltroPedidosAdmin filtros) => Consultar(async () => {
        var model = new PedidosAdminViewModel { Filtros = filtros };
        if (ModelState.IsValid) model.Resultado = await _service.Pedidos(AdministradorId, filtros);
        return View("~/Views/Admin/Pedidos/Index.cshtml", model);
    });

    [HttpGet]
    public Task<IActionResult> DetalhesPedido(int id) => Consultar(async () => {
        var pedido = await _service.DetalhesPedido(AdministradorId, id);
        return pedido == null ? NotFound() : View("~/Views/Admin/Pedidos/Detalhes.cshtml", pedido);
    });

    [HttpPost]
    public async Task<IActionResult> AlterarSituacaoUsuario(AlterarSituacaoUsuarioRequest dados)
    {
        if (!ModelState.IsValid) TempData["AdminErro"] = "Solicitação inválida. Atualize a página e tente novamente.";
        else {
            try {
                await _service.AlterarSituacaoUsuario(AdministradorId, dados.UsuarioId, dados.Ativo!.Value);
                TempData["AdminSucesso"] = dados.Ativo.Value ? "Usuário ativado." : "Usuário desativado. Os pedidos foram preservados.";
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ValidationException ex) { TempData["AdminErro"] = ex.Message; }
            catch (DbUpdateException ex) {
                _logger.LogError(ex, "Falha ao alterar situação do usuário {UsuarioId}.", dados.UsuarioId);
                TempData["AdminErro"] = "Não foi possível salvar. Atualize a página para conferir a situação da conta.";
            }
        }
        // Preserva filtros da listagem, mas nunca redireciona para um site externo.
        return !string.IsNullOrEmpty(dados.Retorno) && Url.IsLocalUrl(dados.Retorno)
            ? LocalRedirect(dados.Retorno) : RedirectToAction(nameof(Usuarios));
    }

}
