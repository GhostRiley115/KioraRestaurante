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
    private readonly KioraRestaurante.Services.RelatoriosAdminService _relatorios;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService service, ILogger<AdminController> logger, KioraRestaurante.Services.RelatoriosAdminService relatorios) { _service = service; _logger = logger; _relatorios = relatorios; }
    private int AdministradorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Centraliza a resposta de acesso negado sem expor erros internos.
    private async Task<IActionResult> Consultar(Func<Task<IActionResult>> acao)
    {
        try { return await acao(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet]
    public Task<IActionResult> Index([FromQuery] FiltroRelatorioAdmin filtro) => Consultar(async () => {
        var painel=await _service.Painel(AdministradorId);
        painel.Relatorio.Filtros=filtro;
        if(ModelState.IsValid) try { painel.Relatorio=await _relatorios.Obter(AdministradorId,filtro); }
        catch(ValidationException ex) { ModelState.AddModelError("",ex.Message); }
        if(!ModelState.IsValid){painel.Relatorio=await _relatorios.Obter(AdministradorId,new());painel.Relatorio.Filtros=filtro;}
        return View(painel);
    });

    [HttpGet]
    public Task<IActionResult> Relatorios([FromQuery] FiltroRelatorioAdmin filtro) => Consultar(async () => {
        if (!ModelState.IsValid) return View(new KioraRestaurante.DTOs.Admin.RelatorioAdminDTO { Filtros=filtro });
        try { return View(await _relatorios.Obter(AdministradorId,filtro)); }
        catch (ValidationException ex) {
            ModelState.AddModelError("",ex.Message);
            return View(new KioraRestaurante.DTOs.Admin.RelatorioAdminDTO { Filtros=filtro });
        }
    });

    [HttpGet]
    public Task<IActionResult> ExportarVendas([FromQuery] FiltroRelatorioAdmin filtro) => Consultar(async () => {
        if (!ModelState.IsValid) return BadRequest("Confira os filtros do relatório.");
        KioraRestaurante.DTOs.Admin.RelatorioAdminDTO relatorio;
        try { relatorio=await _relatorios.Obter(AdministradorId,filtro); }
        catch (ValidationException) { return BadRequest("Confira os filtros do relatório."); }
        var csv=KioraRestaurante.Services.CsvVendasAdmin.Gerar(relatorio.Vendas);
        return File(csv,"text/csv; charset=utf-8",$"vendas-kiora-{filtro.Inicio:yyyyMMdd}-{filtro.Fim:yyyyMMdd}.csv");
    });

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
