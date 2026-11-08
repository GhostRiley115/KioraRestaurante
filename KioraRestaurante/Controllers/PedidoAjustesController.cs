using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Endereco;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Services;
using KioraRestaurante.Services.PagamentosOnline;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize, AutoValidateAntiforgeryToken, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PedidoAjustesController(GerenciarPedidoService service, EnderecoUsuarioService enderecos) : Controller
{
    private int UsuarioId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet]
    public async Task<IActionResult> Endereco(int id, CancellationToken ct)
    {
        try
        {
            var pedido = await service.Buscar(UsuarioId, id, ct);
            if (!GerenciarPedidoService.PodeAlterarEndereco(pedido)) throw new ValidationException("Não é possível alterar o endereço após sair para entrega ou encerrar o pedido.");
            if (pedido.OperacaoPendenteId.HasValue) return RedirectToAction(nameof(Andamento), new { id = pedido.OperacaoPendenteId });
            var e = pedido.EnderecoEntrega;
            var model = new AlterarEnderecoPedidoViewModel { Pedido = pedido, Enderecos = await enderecos.Listar(UsuarioId), Dados = new()
            {
                PedidoId = pedido.Id, VersaoPedido = pedido.Versao, NovoEndereco = new EnderecoCheckoutRequestDTO
                { Cep = e.Cep, Logradouro = e.Logradouro, Numero = e.Numero, Bairro = e.Bairro, Complemento = e.Complemento, Referencia = e.Referencia }
            } };
            return View(model);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ValidationException ex) { TempData["PedidoAviso"] = ex.Message; return RedirectToAction("Detalhes", "Pedido", new { id }); }
    }

    [HttpPost, EnableRateLimiting("cotacao-entrega")]
    public async Task<IActionResult> Cotar([Bind(Prefix = "Dados")] AlterarEnderecoPedidoRequestDTO dados, CancellationToken ct)
    {
        try
        {
            var cotacao = await service.Cotar(UsuarioId, dados.PedidoId, dados.VersaoPedido, dados.SomenteEndereco(), ct);
            return Ok(new { cotacao.Token, cotacao.Taxa, cotacao.DistanciaMetros, cotacao.ExpiraEmUtc,
                cotacao.PreparoMinutos, cotacao.PercursoMinutos, cotacao.PrazoTotalMinutos });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ValidationException ex) { return BadRequest(new { mensagem = ex.Message }); }
    }

    [HttpPost, EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Endereco(AlterarEnderecoPedidoViewModel model, string? acao, CancellationToken ct)
    {
        try
        {
            if (acao == "calcular")
            {
                ModelState.Clear();
                model.Entrega = await service.Cotar(UsuarioId, model.Dados.PedidoId, model.Dados.VersaoPedido, model.Dados.SomenteEndereco(), ct);
                model.Dados.TokenEntrega = model.Entrega.Token;
            }
            else if (ModelState.IsValid)
            {
                var id = await service.AlterarEndereco(UsuarioId, model.Dados, ct);
                return RedirectToAction(nameof(Andamento), new { id });
            }
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is ValidationException or DbUpdateException)
        { ModelState.AddModelError("", ex is ValidationException ? ex.Message : "O pedido mudou. Confira os detalhes antes de tentar novamente."); }
        try { model.Pedido = await service.Buscar(UsuarioId, model.Dados.PedidoId, ct); model.Enderecos = await enderecos.Listar(UsuarioId); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return View(model);
    }

    [HttpPost, EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Cancelar(CancelarPedidoRequestDTO dto, CancellationToken ct)
    {
        try { var id = await service.Cancelar(UsuarioId, dto, ct); return RedirectToAction(nameof(Andamento), new { id }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ValidationException or DbUpdateException)
        { TempData["PedidoAviso"] = ex is ValidationException ? ex.Message : "O pedido mudou. Confira a situação atual."; }
        return RedirectToAction("Detalhes", "Pedido", new { id = dto.PedidoId });
    }

    [HttpGet]
    public async Task<IActionResult> Andamento(Guid id, CancellationToken ct)
    { try { return View(await service.BuscarOperacao(UsuarioId, id, ct)); } catch (KeyNotFoundException) { return NotFound(); } }

    [HttpPost, EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Atualizar(Guid id, CancellationToken ct)
    { try { await service.BuscarOperacao(UsuarioId, id, ct); await service.Processar(id, ct); return RedirectToAction(nameof(Andamento), new { id }); }
      catch (KeyNotFoundException) { return NotFound(); } }

    [HttpPost, EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Pagar(Guid id, CancellationToken ct)
    { try { return Redirect(await service.LinkPagamento(UsuarioId, id, ct)); } catch (KeyNotFoundException) { return NotFound(); }
      catch (Exception ex) when (ex is ValidationException or FalhaPagamento or DbUpdateException)
      { TempData["OperacaoAviso"] = ex is ValidationException ? ex.Message : "Não foi possível abrir a cobrança agora. Confira o andamento antes de tentar novamente."; return RedirectToAction(nameof(Andamento), new { id }); } }

    [HttpPost, EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Desistir(Guid id, CancellationToken ct)
    { try { await service.Desistir(UsuarioId, id, ct); } catch (KeyNotFoundException) { return NotFound(); }
      catch (ValidationException ex) { TempData["OperacaoAviso"] = ex.Message; } return RedirectToAction(nameof(Andamento), new { id }); }
}
