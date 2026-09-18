using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using KioraRestaurante.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class PedidoController : Controller
{
    private readonly IPedidoService _pedidoService;
    private readonly CarrinhoCookie _cookie;

    public PedidoController(IPedidoService pedidoService, CarrinhoCookie cookie)
    {
        _pedidoService = pedidoService;
        _cookie = cookie;
    }

    private int? ObterUsuarioId()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(valor, out var id) && id > 0 ? id : null;
    }

    private AcessoCarrinho ObterAcesso(int usuarioId)
    {
        return new AcessoCarrinho
        {
                UsuarioId = usuarioId,
                CarrinhoVisitanteId = _cookie.Ler(HttpContext)
        };
    }

    private async Task PrepararPagina(CheckoutViewModel model, AcessoCarrinho acesso)
    {
        var resposta =
            await _pedidoService.PrepararCheckout(acesso);

        model.Resumo = resposta.Carrinho;
        model.Dados.TokenRevisao = resposta.TokenRevisao;

        /*
        * Na volta de um POST, os Tag Helpers priorizam valores do ModelState.
        * Removemos apenas o token antigo para que o novo token seja usado no campo oculto.
        */
        ModelState.Remove("Dados.TokenRevisao");
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var usuarioId = ObterUsuarioId();

        if (!usuarioId.HasValue)
            return Unauthorized();

        var model = new CheckoutViewModel();

        try
        {
            await PrepararPagina(model, ObterAcesso(usuarioId.Value));
        }
        catch (RegraPedidoException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var usuarioId = ObterUsuarioId();

        if (!usuarioId.HasValue)
            return Unauthorized();

        var acesso = ObterAcesso(usuarioId.Value);

        if (ModelState.IsValid)
        {
            try
            {
                var pedidoId =
                    await _pedidoService.ConfirmarPedido(acesso, model.Dados);

                return RedirectToAction(nameof(Detalhes), new { id = pedidoId });
            }
            catch (RegraPedidoException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (ValidationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("",
                        "O carrinho mudou durante a confirmação. Confira os dados e tente novamente.");
            }
        }

        try
        {
            await PrepararPagina(model, acesso);
        }
        catch (RegraPedidoException ex)
        {
            // Mantém a confirmação bloqueada.
            model.Resumo = new();
            model.Dados.TokenRevisao = string.Empty;

            ModelState.Remove("Dados.TokenRevisao");
            ModelState.AddModelError("", ex.Message);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        var usuarioId = ObterUsuarioId();

        if (!usuarioId.HasValue)
            return Unauthorized();

        var pedido = await _pedidoService.BuscarPedido(usuarioId.Value, id);

        if (pedido == null)
            return NotFound();

        return View(pedido);
    }

    [HttpGet]
    public async Task<IActionResult> MeusPedidos()
    {
        var usuarioId = ObterUsuarioId();

        if (!usuarioId.HasValue)
            return Unauthorized();

        var pedidos = await _pedidoService.ListarPedidos(
            usuarioId.Value);

        return View(pedidos);
    }

}
