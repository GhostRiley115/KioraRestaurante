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
using KioraRestaurante.ViewModels.Admin;

namespace KioraRestaurante.Controllers;

// Somente Admin tem acesso a esse controller.
[Authorize(Roles = nameof(TipoUsuario.Administrador))]
[AutoValidateAntiforgeryToken]
// Isso orienta o navegador a não guardar essas páginas administrativas em cache.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ProdutosAdminController : Controller
{
    // As telas administrativas ficam agrupadas por funcionalidade.
    private const string ViewCadastro = "~/Views/Admin/Produtos/Cadastrar.cshtml";

    private readonly IProdutoService _produtoService;
    private readonly ICategoriaService _categoriaService;

    private const string ViewLista = "~/Views/Admin/Produtos/Index.cshtml";
    private const string ViewEdicao = "~/Views/Admin/Produtos/Editar.cshtml";
    private int AdministradorId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

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

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FiltroProdutosAdminDTO filtro)
    {
        var model = new ProdutosAdminViewModel
        {
            Filtros = filtro
        };

        if (!ModelState.IsValid)
        {
            return View(ViewLista, model);
        }

        try
        {
            model.Resultado = await _produtoService.ListarAdmin(
                AdministradorId,
                filtro);

            return View(ViewLista, model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var produto = await _produtoService.BuscarAdmin(AdministradorId, id);

            if (produto == null)
            {
                return NotFound();
            }

            var model = new EditarProdutoViewModel
            {
                Id = produto.Id,
                Nome = produto.Nome,
                Descricao = produto.Descricao,
                PrecoTexto = produto.Preco.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")),
                CategoriaId = produto.CategoriaId,
                ImagemAtual = produto.ImagemUrl,
                Categorias = await _categoriaService.ListarTodas()
            };

            return View(ViewEdicao, model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Editar(EditarProdutoViewModel model)
    {
        try
        {
            var produto = await _produtoService.BuscarAdmin(AdministradorId, model.Id);

            if (produto == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var precoValido = decimal.TryParse(
                    model.PrecoTexto.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var preco);

                if (!precoValido)
                {
                    ModelState.AddModelError(
                        nameof(model.PrecoTexto),
                        "Informe um preço válido.");
                }
                else
                {
                    var dto = new EditarProdutoRequestDTO
                    {
                        Nome = model.Nome,
                        Descricao = model.Descricao,
                        Preco = preco,
                        CategoriaId = model.CategoriaId
                    };

                    try
                    {
                        await _produtoService.Editar(AdministradorId, model.Id, dto, model.Foto);

                        TempData["AdminSucesso"] = "Produto atualizado com sucesso.";

                        return RedirectToAction(nameof(Index));
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
                        ModelState.AddModelError("",
                            "Não foi possível confirmar a edição. " +
                            "Confira o produto antes de tentar novamente.");
                    }
                }
            }

            // Reconstrói os dados que não são enviados pelo formulário.
            model.ImagemAtual = produto.ImagemUrl;
            model.Categorias = await _categoriaService.ListarTodas();

            return View(ViewEdicao, model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public Task<IActionResult> AlterarAtivo(int id, bool? valor)
    {
        return AlterarSituacao(
            id,
            valor,
            _produtoService.AlterarAtivo,
            valor == true
                ? "Produto reativado. Confira a disponibilidade antes de vender."
                : "Produto desativado e retirado do cardápio.");
    }

    [HttpPost]
    public Task<IActionResult> AlterarDisponibilidade(int id, bool? valor)
    {
        return AlterarSituacao(
            id,
            valor,
            _produtoService.AlterarDisponibilidade,
            valor == true
                ? "Produto disponível para compra."
                : "Produto marcado como indisponível.");
    }

    // Centraliza o tratamento que seria igual nas duas ações.
    private async Task<IActionResult> AlterarSituacao(
        int id,
        bool? valor,
        Func<int, int, bool, Task> alterar,
        string mensagem)
    {
        if (!ModelState.IsValid || id <= 0 || !valor.HasValue)
        {
            return BadRequest();
        }

        try
        {
            await alterar(AdministradorId, id, valor.Value);

            TempData["AdminSucesso"] = mensagem;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (RegraProdutoException ex)
        {
            TempData["AdminErro"] = ex.Message;
        }
        catch (DbUpdateException)
        {
            TempData["AdminErro"] = "Não foi possível confirmar a alteração. Confira o produto antes de repetir.";
        }

        return RedirectToAction(nameof(Index));
    }
}