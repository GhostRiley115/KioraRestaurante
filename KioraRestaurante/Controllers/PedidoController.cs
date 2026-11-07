using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using KioraRestaurante.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Services.Entrega;
using Microsoft.AspNetCore.RateLimiting;

namespace KioraRestaurante.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PedidoController : Controller
{
    private readonly IPedidoService _pedidoService;
    private readonly CarrinhoCookie _cookie;
    private readonly EnderecoUsuarioService _enderecoService;
    private readonly CotacaoEntregaService _entregaService;

    public PedidoController(IPedidoService pedidoService, CarrinhoCookie cookie, EnderecoUsuarioService enderecoService,
        CotacaoEntregaService entregaService)
    {
        _pedidoService = pedidoService;
        _cookie = cookie;
        _enderecoService = enderecoService;
        _entregaService = entregaService;
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
        // Recarrega as opções tanto no GET quanto na volta de um POST com erro.
        model.Enderecos = await _enderecoService.Listar(
            acesso.UsuarioId!.Value);

        var resposta =
            await _pedidoService.PrepararCheckout(acesso);

        model.Resumo = resposta.Carrinho;
        model.Dados.TokenRevisao = resposta.TokenRevisao;

        /*
        * Na volta de um POST, os Tag Helpers priorizam valores do ModelState.
        * Removemos apenas o token antigo para que o novo token seja usado no campo oculto.
        */
        ModelState.Remove("Dados.TokenRevisao");

        if (model.Entrega == null && !string.IsNullOrWhiteSpace(model.Dados.TokenEntrega))
        {
            try
            {
                var endereco = await _enderecoService.PrepararParaCheckout(acesso.UsuarioId.Value, model.Dados);
                model.Entrega = _entregaService.Conferir(acesso.UsuarioId.Value, endereco.Entrega, model.Dados.TokenEntrega);
            }
            catch (ValidationException)
            {
                model.Dados.TokenEntrega = "";
                ModelState.Remove("Dados.TokenEntrega");
            }
        }
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
            // Seleção inicial apenas ao abrir uma nova revisão.
            var primeiro = model.Enderecos.FirstOrDefault();

            if (primeiro != null)
            {
                model.Dados.EnderecoId = primeiro.Id;
                model.Dados.EnderecoVersao = primeiro.Versao;
            }
        }
        catch (RegraPedidoException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return View(model);
    }

    [HttpPost]
    [EnableRateLimiting("cotacao-entrega")]
    public async Task<IActionResult> Checkout(CheckoutViewModel model, string? acao = null)
    {
        var usuarioId = ObterUsuarioId();

        if (!usuarioId.HasValue)
            return Unauthorized();

        var acesso = ObterAcesso(usuarioId.Value);

        if (acao == "calcular")
        {
            // Sem JavaScript, calcula a entrega na mesma página, antes da confirmação.
            ModelState.Clear();
            try
            {
                var endereco = await _enderecoService.PrepararParaCheckout(usuarioId.Value, model.Dados);
                model.Entrega = await _entregaService.Cotar(usuarioId.Value, endereco.Entrega, HttpContext.RequestAborted);
                model.Dados.TokenEntrega = model.Entrega.Token;
            }
            catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            try { await PrepararPagina(model, acesso); }
            catch (RegraPedidoException ex)
            {
                model.Resumo = new();
                model.Dados.TokenRevisao = "";
                ModelState.AddModelError("", ex.Message);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            return View(model);
        }

        if (ModelState.IsValid)
        {
            try
            {
                var pedidoId =
                    await _pedidoService.ConfirmarPedido(acesso, model.Dados);

                if (model.Dados.FormaPagamento == KioraRestaurante.Models.Enums.FormaPagamento.MercadoPago)
                    return RedirectToAction("Index", "Pagamento", new { id = pedidoId });

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
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Não foi possível confirmar o pedido. Confira os dados e tente novamente.");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return View(model);
    }

    [HttpPost]
    [EnableRateLimiting("cotacao-entrega")]
    public async Task<IActionResult> CotarEntrega([Bind(Prefix = "Dados")] CotarEntregaRequestDTO dados)
    {
        var usuarioId = ObterUsuarioId();
        if (!usuarioId.HasValue) return Unauthorized();
        try
        {
            var endereco = await _enderecoService.PrepararParaCheckout(usuarioId.Value, dados);
            var cotacao = await _entregaService.Cotar(usuarioId.Value, endereco.Entrega, HttpContext.RequestAborted);
            return Ok(new { cotacao.Token, cotacao.Taxa, cotacao.DistanciaMetros, cotacao.ExpiraEmUtc,
                cotacao.PreparoMinutos, cotacao.PercursoMinutos, cotacao.PrazoTotalMinutos });
        }
        catch (ValidationException ex) { return BadRequest(new { mensagem = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
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
