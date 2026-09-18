using KioraRestaurante.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;

namespace KioraRestaurante.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProdutoService _produtoService;
        private readonly ICategoriaService _categoriaService;

        public HomeController(IProdutoService produtoService, ICategoriaService categoriaService)
        {
            _produtoService = produtoService;
            _categoriaService = categoriaService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Sobre()
        {
            return View();
        }

        // Retorna os produtos da categoria selecionada pelo usuário.
        [HttpGet]
        public async Task<IActionResult> Cardapio(int? categoriaId)
        {
            var categorias = await _categoriaService.ListarTodas();

            var selecionada = categorias.SingleOrDefault(
                c => c.Id == categoriaId);

            if (categoriaId.HasValue && selecionada == null)
            {
                return NotFound();
            }

            var model = new CardapioViewModel
            {
                Categorias = categorias,
                Produtos = await _produtoService.ListarCardapio(categoriaId),
                CategoriaSelecionadaId = categoriaId,
                Titulo = selecionada?.Nome ?? "Todos os produtos"
            };

            return View(model);
        }

        public IActionResult Galeria()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
