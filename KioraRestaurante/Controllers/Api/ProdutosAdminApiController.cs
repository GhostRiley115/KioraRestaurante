using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services.Produtos;

namespace KioraRestaurante.Controllers.Api
{
    // Controller responsável pelo gerenciamento administrativo de produtos.
    [ApiController]
    [Route("api/admin/produtos")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Roles = nameof(TipoUsuario.Administrador))]
    public class ProdutosAdminApiController : ControllerBase
    {
        private readonly IProdutoService _produtoService;

        public ProdutosAdminApiController(IProdutoService produtoService)
        {
            _produtoService = produtoService;
        }

        // Recupera o ID do administrador autenticado pelo JWT.
        private bool TentarObterAdministradorId(out int administradorId)
        {
            var idTexto = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(idTexto, out administradorId);
        }

        // GET: api/admin/produtos
        // GET: api/admin/produtos?busca=lamen&ativo=true&pagina=1
        // Lista os produtos com filtros e paginação.
        [HttpGet]
        public async Task<ActionResult<ResultadoPaginadoDTO<ProdutoAdminResponseDTO>>>
            ListarProdutos([FromQuery] FiltroProdutosAdminDTO filtro)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            try
            {
                var resultado = await _produtoService.ListarAdmin(
                    administradorId,
                    filtro);

                return Ok(resultado);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
        }


        // GET: api/admin/produtos/1
        // Busca os dados completos de um produto para edição.
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProdutoAdminResponseDTO>>
            BuscarProduto(int id)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um ID de produto válido."
                });
            }

            try
            {
                var produto = await _produtoService.BuscarAdmin(
                    administradorId,
                    id);

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
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // POST: api/admin/produtos
        // Cadastra um produto com sua foto.
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProdutoAdminResponseDTO>>
            CadastrarProduto(
                [FromForm] CriarProdutoRequestDTO dto,
                IFormFile? foto)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (foto == null || foto.Length == 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "A foto do produto é obrigatória."
                });
            }

            try
            {
                var produtoId = await _produtoService.Criar(
                    administradorId,
                    dto,
                    foto);

                var produto = await _produtoService.BuscarAdmin(
                    administradorId,
                    produtoId);

                return CreatedAtAction(
                    nameof(BuscarProduto),
                    new { id = produtoId },
                    produto);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (RegraProdutoException ex)
            {
                return BadRequest(new { sucesso = false, mensagem = ex.Message });
            }
            catch (DbUpdateException)
            {
                return FalhaGravacao();
            }
        }

        // PUT: api/admin/produtos/1
        // Atualiza os dados de um produto.
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EditarProduto(
            int id,
            [FromForm] EditarProdutoRequestDTO dto,
            IFormFile? foto)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um ID de produto válido."
                });
            }

            try
            {
                await _produtoService.Editar(
                    administradorId,
                    id,
                    dto,
                    foto);

                var produto = await _produtoService.BuscarAdmin(
                    administradorId,
                    id);

                return Ok(produto);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Produto não encontrado."
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (RegraProdutoException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (DbUpdateException)
            {
                return FalhaGravacao();
            }
        }
        // PATCH: api/admin/produtos/1/ativo
        // Ativa ou desativa um produto.
        [HttpPatch("{id:int}/ativo")]
        public async Task<IActionResult> AlterarAtivo(
            int id,
            [FromBody] AlterarStatusProdutoRequestDTO dto)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um ID de produto válido."
                });
            }

            if (!ModelState.IsValid || !dto.Ativo.HasValue)
                return BadRequest(new { sucesso = false, mensagem = "Informe se o produto deve ficar ativo." });

            try
            {
                await _produtoService.AlterarAtivo(
                    administradorId,
                    id,
                    dto.Ativo.Value);

                return Ok(new
                {
                    sucesso = true,
                    mensagem = dto.Ativo.Value
                        ? "Produto ativado com sucesso."
                        : "Produto desativado com sucesso."
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Produto não encontrado."
                });
            }
            catch (RegraProdutoException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (DbUpdateException)
            {
                return FalhaGravacao();
            }
        }
        // PATCH: api/admin/produtos/1/disponibilidade
        // Altera a disponibilidade do produto para pedidos.
        [HttpPatch("{id:int}/disponibilidade")]
        public async Task<IActionResult> AlterarDisponibilidade(
            int id,
            [FromBody] AlterarDisponibilidadeProdutoRequestDTO dto)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um ID de produto válido."
                });
            }

            if (!ModelState.IsValid || !dto.Disponivel.HasValue)
                return BadRequest(new { sucesso = false, mensagem = "Informe se o produto está disponível." });

            try
            {
                await _produtoService.AlterarDisponibilidade(
                    administradorId,
                    id,
                    dto.Disponivel.Value);

                return Ok(new
                {
                    sucesso = true,
                    mensagem = dto.Disponivel.Value
                        ? "Produto disponibilizado com sucesso."
                        : "Produto indisponibilizado com sucesso."
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Produto não encontrado."
                });
            }
            catch (RegraProdutoException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (DbUpdateException)
            {
                return FalhaGravacao();
            }
        }
        // PATCH: api/admin/produtos/1/desconto
        // Aplica ou remove o desconto de um produto.
        [HttpPatch("{id:int}/desconto")]
        public async Task<IActionResult> AlterarDesconto(
            int id,
            [FromBody] AlterarDescontoProdutoApiRequestDTO dto)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            if (id <= 0)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um ID de produto válido."
                });
            }

            if (!ModelState.IsValid || !dto.Percentual.HasValue)
                return BadRequest(new { sucesso = false, mensagem = "Informe o percentual de desconto. Use zero para removê-lo." });

            try
            {
                await _produtoService.AlterarDesconto(
                    administradorId,
                    id,
                    new AlterarDescontoRequestDTO { Percentual = dto.Percentual.Value });

                var produto = await _produtoService.BuscarAdmin(
                    administradorId,
                    id);

                return Ok(produto);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Produto não encontrado."
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (RegraProdutoException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
            catch (DbUpdateException)
            {
                return FalhaGravacao();
            }
        }
                // GET: api/admin/produtos/opcoes-componentes
        // Lista os produtos que podem compor um combo.
        [HttpGet("opcoes-componentes")]
        public async Task<ActionResult<List<OpcaoComponenteDTO>>>
            ListarOpcoesComponentes([FromQuery] int? produtoAtualId)
        {
            if (!TentarObterAdministradorId(out var administradorId))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o administrador."
                });
            }

            try
            {
                var opcoes = await _produtoService.ListarOpcoesComponentes(
                    administradorId,
                    produtoAtualId);

                return Ok(opcoes);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ex.Message
                });
            }
        }
        private ConflictObjectResult FalhaGravacao() => Conflict(new
        {
            sucesso = false,
            mensagem = "Não foi possível confirmar a alteração. Consulte os dados antes de tentar novamente."
        });

    }
}
