using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

// Somente Admin tem acesso a esse controller.
[Authorize(Roles = nameof(TipoUsuario.Administrador))]
[AutoValidateAntiforgeryToken]
public class ProdutosAdminController : Controller
{
    // As telas administrativas ficam agrupadas por funcionalidade.
    private const string ViewCadastro = "~/Views/Admin/Produtos/Cadastrar.cshtml";

    private readonly IProdutoService _produtoService;
    private readonly ICategoriaService _categoriaService;

    public ProdutosAdminController(
        IProdutoService produtoService,
        ICategoriaService categoriaService)
    {
        _produtoService = produtoService;
        _categoriaService = categoriaService;
    }

    // Retorna as categorias já existentes.
    [HttpGet]
    public async Task<IActionResult> Cadastrar()
    {
        var model = new CadastrarProdutoViewModel
        {
            Categorias = await _categoriaService.ListarTodas()
        };

        return View(ViewCadastro, model);
    }

    // Cadastra um novo produto pela tela de Admin.
    [HttpPost]
    // Limite da imagem enviada.
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Cadastrar(CadastrarProdutoViewModel model)
    {
        var identificador = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(identificador, out var usuarioId) || usuarioId <= 0)
        {
            return Unauthorized();
        }

        if (ModelState.IsValid)
        {
            var texto = model.PrecoTexto.Replace(',', '.');

            var precoValido = decimal.TryParse(
                texto,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var preco);

            if (!precoValido)
            {
                ModelState.AddModelError(nameof(model.PrecoTexto), "Informe um preço válido.");
            }
            else
            {
                var dto = new CriarProdutoRequestDTO
                {
                    Nome = model.Nome,
                    Descricao = model.Descricao,
                    Preco = preco,
                    CategoriaId = model.CategoriaId,
                    Disponivel = model.Disponivel
                };

                try
                {
                    await _produtoService.Criar(usuarioId, dto, model.Foto!);

                    TempData["ProdutoSucesso"] = "Produto cadastrado com sucesso.";

                    return RedirectToAction(
                        "Cardapio",
                        "Home",
                        new { categoriaId = model.CategoriaId });
                }
                catch (UnauthorizedAccessException)
                {
                    return StatusCode(403);
                }
                catch (RegraProdutoException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (ValidationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Não foi possível confirmar o cadastro. " +
                                                 "Confira o cardápio antes de tentar novamente.");
                }
            }
        }

        // Na volta com erro, precisamos montar o select novamente.
        model.Categorias = await _categoriaService.ListarTodas();

        return View(ViewCadastro, model);
    }
}