using KioraRestaurante.DTOs.Categoria;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.Services.Cardapio;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers.Api
{
    // Controller responsável por disponibilizar o cardápio para o aplicativo.
    // As consultas são públicas e não exigem autenticação.
    [ApiController]
    [Route("api/cardapio")]
    [AllowAnonymous]
    public class CardapioApiController : ControllerBase
    {
        private readonly ICategoriaService _categoriaService;
        private readonly IProdutoService _produtoService;
        private readonly MaisPedidosService _maisPedidosService;

        public CardapioApiController(
            ICategoriaService categoriaService,
            IProdutoService produtoService,
            MaisPedidosService maisPedidosService)
        {
            _categoriaService = categoriaService;
            _produtoService = produtoService;
            _maisPedidosService = maisPedidosService;
        }

        // GET: api/cardapio/categorias
        // Retorna as categorias disponíveis no cardápio.
        [HttpGet("categorias")]
        public async Task<ActionResult<List<CategoriaResponseDTO>>> ListarCategorias()
        {
            var categorias = await _categoriaService.ListarTodas();

            return Ok(categorias.OrderBy(c => ConsultaCardapio.OrdemCategoria(c.Nome))
                .ThenBy(c => c.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true))
                .ToList());
        }

        // GET: api/cardapio/produtos
        // GET: api/cardapio/produtos?categoriaId=1
        // Retorna os produtos, permitindo filtrar por categoria.
        [HttpGet("produtos")]
        public async Task<ActionResult<List<ProdutoResponseDTO>>> ListarProdutos(
            [FromQuery] int? categoriaId,
            [FromQuery] FiltroCardapioViewModel? filtros = null,
            CancellationToken ct = default)
        {
            filtros ??= new FiltroCardapioViewModel();
            var categorias = await _categoriaService.ListarTodas();
            // Verifica se a categoria informada existe.
            if (categoriaId.HasValue)
            {
                if (!categorias.Any(c => c.Id == categoriaId.Value))
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Categoria não encontrada."
                    });
                }
            }

            // A API compartilha filtros, limites e nomes de coleções com o cardápio web.
            var validacao = ConsultaCardapio.Montar([], categorias, categoriaId, filtros);
            if (validacao.ErrosFiltros.Count > 0)
                return BadRequest(new { sucesso = false, mensagem = string.Join(" ", validacao.ErrosFiltros), erros = validacao.ErrosFiltros });

            RankingMaisPedidos? ranking = null;
            if (filtros.Colecao == ColecoesCardapio.MaisPedidos)
                ranking = await _maisPedidosService.Consultar(ct);

            // Reutiliza o serviço responsável pelo cardápio do site.
            var produtos = await _produtoService.ListarCardapio(categoriaId);
            var resultado = ConsultaCardapio.Montar(produtos, categorias, categoriaId, filtros, ranking?.Quantidades);
            // Mantém a resposta como lista; a ordem padrão acompanha as seções exibidas no site.
            return Ok(resultado.ExibirSeparadores
                ? resultado.Secoes.SelectMany(s => s.Produtos).ToList()
                : resultado.Produtos);
        }

        // GET: api/cardapio/produtos/1
        // Retorna os detalhes de um produto específico.
        [HttpGet("produtos/{id:int}")]
        public async Task<ActionResult<ProdutoResponseDTO>> BuscarProduto(int id)
        {
            // Consulta os produtos disponíveis no cardápio.
            var produto = await _produtoService.BuscarCardapio(id);

            if (produto == null)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Produto não encontrado."
                });
            }

            return Ok(produto);
        }
    }
}
