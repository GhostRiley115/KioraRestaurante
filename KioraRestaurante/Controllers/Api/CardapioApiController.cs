using KioraRestaurante.DTOs.Categoria;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Services.Interfaces;
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

        public CardapioApiController(
            ICategoriaService categoriaService,
            IProdutoService produtoService)
        {
            _categoriaService = categoriaService;
            _produtoService = produtoService;
        }

        // GET: api/cardapio/categorias
        // Retorna as categorias disponíveis no cardápio.
        [HttpGet("categorias")]
        public async Task<ActionResult<List<CategoriaResponseDTO>>> ListarCategorias()
        {
            var categorias = await _categoriaService.ListarTodas();

            return Ok(categorias);
        }

        // GET: api/cardapio/produtos
        // GET: api/cardapio/produtos?categoriaId=1
        // Retorna os produtos, permitindo filtrar por categoria.
        [HttpGet("produtos")]
        public async Task<ActionResult<List<ProdutoResponseDTO>>> ListarProdutos(
            [FromQuery] int? categoriaId)
        {
            // Verifica se a categoria informada existe.
            if (categoriaId.HasValue)
            {
                var categorias = await _categoriaService.ListarTodas();

                if (!categorias.Any(c => c.Id == categoriaId.Value))
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = "Categoria não encontrada."
                    });
                }
            }

            // Reutiliza o serviço responsável pelo cardápio do site.
            var produtos = await _produtoService.ListarCardapio(categoriaId);

            return Ok(produtos);
        }

        // GET: api/cardapio/produtos/1
        // Retorna os detalhes de um produto específico.
        [HttpGet("produtos/{id:int}")]
        public async Task<ActionResult<ProdutoResponseDTO>> BuscarProduto(int id)
        {
            // Consulta os produtos disponíveis no cardápio.
            var produtos = await _produtoService.ListarCardapio();

            var produto = produtos.FirstOrDefault(p => p.Id == id);

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
