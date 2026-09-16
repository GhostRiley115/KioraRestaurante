using KioraRestaurante.Models;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers
{
    public class ProdutoController : Controller
    {
        private readonly IProdutoService _produtoService;
        private readonly ICategoriaService _categoriaService;


        // Padrão de injeção de dependências 
        public ProdutoController(
            IProdutoService produtoService,
            ICategoriaService categoriaService)
        {
            _produtoService = produtoService;
            _categoriaService = categoriaService;
        }
        
        [HttpGet]
        public async Task<IActionResult> Criar()
        {
            var model = new CriarProdutoViewModel
            {
                CategoriasDisponiveis = await ObterCategorias()
            };

            return View(model);
        }
        //O modelState é um objeto que o asp preenche sózinho
        // checando se a model recebida respeita os atributos para a validação
        [HttpPost]
        public async Task<IActionResult> Criar(CriarProdutoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.CategoriasDisponiveis = await ObterCategorias();
                return View(model);
            }

            var produto = new Produto
            {
                Nome = model.Nome,
                Descricao = model.Descricao ?? string.Empty,
                Preco = model.Preco,
                Imagem = model.Imagem,
                CategoriaId = model.CategoriaId

            };

            await _produtoService.Criar(produto);
            return RedirectToAction("Index", "Home");
        }

        // o método private não possibilita o acesso por nenhunha url
        // só existe para ser chamada dentro da própria classe
        private async Task<List<SelectListItem>> ObterCategorias()
        {
            var categorias = await _categoriaService.ListarTodas();

            return categorias
                .Select(c => new SelectListItem(c.Nome, c.Id.ToString()))
                .ToList();
        }
    }
}
