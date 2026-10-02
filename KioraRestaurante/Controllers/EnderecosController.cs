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