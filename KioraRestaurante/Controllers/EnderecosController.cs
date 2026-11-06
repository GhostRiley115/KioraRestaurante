using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Endereco;
using KioraRestaurante.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class EnderecosController : Controller
{
    private readonly EnderecoUsuarioService _service;

    public EnderecosController(EnderecoUsuarioService service)
    {
        _service = service;
    }

    private int UsuarioId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await _service.Listar(UsuarioId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public IActionResult Novo()
    {
        return View("Formulario", new SalvarEnderecoRequestDTO());
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var endereco = await _service.Buscar(UsuarioId, id);

            if (endereco == null)
                return NotFound();

            ViewData["EnderecoId"] = id;

            return View("Formulario", new SalvarEnderecoRequestDTO
            {
                Apelido = endereco.Apelido,
                Cep = endereco.Cep,
                Logradouro = endereco.Logradouro,
                Numero = endereco.Numero,
                Bairro = endereco.Bairro,
                Complemento = endereco.Complemento,
                Referencia = endereco.Referencia,
                Versao = endereco.Versao
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Excluir(int id, Guid versao)
    {
        var ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        try
        {
            if (!ModelState.IsValid || versao == Guid.Empty)
                throw new ValidationException("Atualize a página antes de excluir o endereço.");
            await _service.Excluir(UsuarioId, id, versao);
            if (ajax) return Ok(new { mensagem = "Endereço excluído. Os pedidos anteriores foram preservados." });
            TempData["EnderecoSucesso"] = "Endereço excluído. Os pedidos anteriores foram preservados.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException)
        {
            if (ajax) return NotFound(new { mensagem = "Esse endereço não está disponível na sua conta. Atualize a página." });
            TempData["EnderecoErro"] = "Esse endereço não está disponível na sua conta.";
        }
        catch (Exception ex) when (ex is ValidationException or DbUpdateConcurrencyException or DbUpdateException)
        {
            var mensagem = ex is ValidationException ? ex.Message : "O endereço mudou. Atualize a página antes de tentar novamente.";
            if (ajax) return BadRequest(new { mensagem });
            TempData["EnderecoErro"] = mensagem;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Salvar(int? id, SalvarEnderecoRequestDTO dto)
    {
        ViewData["EnderecoId"] = id;

        if (!ModelState.IsValid)
            return View("Formulario", dto);

        try
        {
            await _service.Salvar(UsuarioId, id, dto);

            TempData["EnderecoSucesso"] = "Endereço salvo.";

            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(
                "",
                "Esse endereço mudou em outra operação. " +
                "Volte à listagem e abra a edição novamente.");
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "Não foi possível confirmar o salvamento. " +
                "Confira seus endereços antes de repetir.");
        }

        return View("Formulario", dto);
    }
}
