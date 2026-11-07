using KioraRestaurante.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using KioraRestaurante.Services.Cardapio;

namespace KioraRestaurante.Controllers
{
    public class HomeController : Controller
    {
        private readonly MaisPedidosService _maisPedidosService;
        private readonly IProdutoService _produtoService;
        private readonly ICategoriaService _categoriaService;

        public HomeController(IProdutoService produtoService, ICategoriaService categoriaService, MaisPedidosService maisPedidosService)
        {
            _produtoService = produtoService;
            _categoriaService = categoriaService;
            _maisPedidosService = maisPedidosService;
        }

        public IActionResult Index()
        {
            if (User.IsInRole("Gerente") || User.IsInRole("Garcom") || User.IsInRole("Portaria") || User.IsInRole("Cozinha"))
                return RedirectToAction("Index", "Restaurante");
            return View();
        }

        // Retorna 403 sem expor informações internas das páginas protegidas.
        public IActionResult AcessoNegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        public IActionResult Sobre()
        {
            return View();
        }

        // Retorna os produtos da categoria selecionada pelo usuário.
        [HttpGet]
        public async Task<IActionResult> Cardapio(int? categoriaId,
            [FromQuery, Bind(Prefix = "")] FiltroCardapioViewModel filtros)
        {
            filtros.Colecao = ColecoesCardapio.Normalizar(filtros.Colecao);
            if (filtros.Colecao != "" && !ColecoesCardapio.Nomes.ContainsKey(filtros.Colecao))
                return NotFound();
            if (categoriaId.HasValue && filtros.Colecao != "")
                return BadRequest("Escolha uma categoria ou uma coleção por vez.");

            var categorias = await _categoriaService.ListarTodas();
            if (categoriaId.HasValue && categorias.All(c => c.Id != categoriaId.Value))
                return NotFound();

            var produtos = await _produtoService.ListarCardapio(categoriaId);
            RankingMaisPedidos? ranking = null;
            if (filtros.Colecao == ColecoesCardapio.MaisPedidos && ModelState.IsValid)
                ranking = await _maisPedidosService.Consultar(HttpContext.RequestAborted);

            var model = ConsultaCardapio.Montar(
                produtos, categorias, categoriaId, filtros, ranking?.Quantidades);

            if (ranking != null)
            {
                var inicio = ranking.Periodo.InicioLocal;
                var ultimoDia = ranking.Periodo.FimLocalExclusivo.AddDays(-1);
                model.PeriodoMaisPedidos = $"Mais pedidos de {inicio:dd/MM/yyyy} a {ultimoDia:dd/MM/yyyy}.";
            }

            if (!ModelState.IsValid)
            {
                model.ErrosFiltros.Add("Há um parâmetro inválido no endereço. Limpe os filtros e tente novamente.");
                model.Produtos.Clear();
                model.Secoes.Clear();
            }
            return View(model);
        }
                
        public IActionResult Galeria()
        {
            return View();
        }

        // =========================================================
        // PÁGINA DE TERMOS DE USO
        // =========================================================

        public IActionResult TermosUso()
        {
            return View();
        }

        // =========================================================
        // PÁGINA DE POLÍTICA DE PRIVACIDADE
        // =========================================================

        public IActionResult PoliticaPrivacidade()
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
