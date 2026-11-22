using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Produtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

// Gerenciamento administrativo das categorias do cardápio para o Kiora Mobile.
// Permite cadastrar, editar, consultar, ativar e desativar categorias.

// Preserva os produtos vinculados e não realiza exclusão física.

[ApiController]
[Route("api/admin/categorias")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Authorize(
    AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public sealed class CategoriasAdminApiController
    : ControllerBase
{
    private readonly CategoriaAdminService _categorias;

    private readonly ILogger<CategoriasAdminApiController>
        _logger;

    public CategoriasAdminApiController(
        CategoriaAdminService categorias,
        ILogger<CategoriasAdminApiController> logger)
    {
        _categorias = categorias;
        _logger = logger;
    }

    // LISTAR CATEGORIAS

    // Lista categorias ativas e inativas, incluindo indicadores de produtos vinculados.
    // GET /api/admin/categorias
    // GET /api/admin/categorias?ativa=true
    // GET /api/admin/categorias?ativa=false
    
    [HttpGet]
    public Task<IActionResult> Listar(
        [FromQuery] bool? ativa,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            var categorias = await _categorias.Listar(
                administradorId,
                ativa,
                ct);

            return Ok(new
            {
                sucesso = true,

                total = categorias.Count,

                totalAtivas =
                    categorias.Count(c => c.Ativa),

                totalInativas =
                    categorias.Count(c => !c.Ativa),

                categorias
            });
        });
    }

    // BUSCAR CATEGORIA POR ID

    // Consulta os detalhes administrativos de uma categoria.
    // GET /api/admin/categorias/{id}
   
    [HttpGet("{id:int}")]
    public Task<IActionResult> Buscar(
        int id,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var categoria = await _categorias.Buscar(
                administradorId,
                id,
                ct);

            return Ok(new
            {
                sucesso = true,

                categoria
            });
        });
    }

    // CADASTRAR CATEGORIA
 
    // Cadastra uma nova categoria ativa.
    // POST /api/admin/categorias
    
    [HttpPost]
    public Task<IActionResult> Cadastrar(
        [FromBody] CategoriaMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            var categoria =
                await _categorias.Cadastrar(
                    administradorId,
                    dto.Nome,
                    ct);

            return CreatedAtAction(
                nameof(Buscar),

                new
                {
                    id = categoria.Id
                },

                new
                {
                    sucesso = true,

                    mensagem =
                        "Categoria cadastrada " +
                        "com sucesso.",

                    categoria
                });
        });
    }

    // EDITAR CATEGORIA

    // Altera o nome de uma categoria existente.
    // PUT /api/admin/categorias/{id}
    
    [HttpPut("{id:int}")]
    public Task<IActionResult> Editar(
        int id,
        [FromBody] CategoriaMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var categoria = await _categorias.Editar(
                administradorId,
                id,
                dto.Nome,
                ct);

            return Ok(new
            {
                sucesso = true,

                mensagem =
                    "Categoria atualizada " +
                    "com sucesso.",

                categoria
            });
        });
    }

    // ATIVAR OU DESATIVAR CATEGORIA

    // Altera a situação de uma categoria.
    // Não permite desativar categorias que possuam produtos ativos vinculados.
    // PATCH /api/admin/categorias/{id}/ativa
    
    [HttpPatch("{id:int}/ativa")]
    public Task<IActionResult> AlterarAtiva(
        int id,
        [FromBody] CategoriaSituacaoMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            if (!dto.Ativa.HasValue)
            {
                throw new ValidationException(
                    "Informe a situação desejada " +
                    "para a categoria.");
            }

            var categoria =
                await _categorias.AlterarAtiva(
                    administradorId,
                    id,
                    dto.Ativa.Value,
                    ct);

            return Ok(new
            {
                sucesso = true,

                mensagem = categoria.Ativa
                    ? "Categoria ativada com sucesso."
                    : "Categoria desativada com sucesso.",

                categoria
            });
        });
    }

    // VALIDAÇÃO DO IDENTIFICADOR
    private static void ValidarId(int id)
    {
        if (id <= 0)
        {
            throw new ValidationException(
                "Informe um ID de categoria válido.");
        }
    }

    // IDENTIFICAÇÃO DO ADMINISTRADOR
    private bool TentarObterAdministradorId(
        out int administradorId)
    {
        var identificacao = User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return int.TryParse(
            identificacao,
            out administradorId)
            && administradorId > 0;
    }

    // TRATAMENTO PADRONIZADO DAS OPERAÇÕES
    private async Task<IActionResult> Executar(
        Func<int, Task<IActionResult>> operacao)
    {
        if (!TentarObterAdministradorId(
                out var administradorId))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível identificar " +
                    "o administrador."
            });
        }

        try
        {
            return await operacao(administradorId);
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

                mensagem =
                    "Categoria não encontrada."
            });
        }
        catch (ValidationException erro)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem = erro.Message
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "Os dados foram alterados durante " +
                    "a operação. Atualize a consulta " +
                    "antes de tentar novamente.",

                atualizarDados = true
            });
        }
        catch (DbUpdateException erro)
        {
            _logger.LogError(
                erro,
                "Falha durante operação administrativa " +
                "de categoria. Administrador: {AdministradorId}.",
                administradorId);

            return StatusCode(500, new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível salvar " +
                    "os dados da categoria.",

                atualizarDados = true
            });
        }
    }
}

// CONTRATOS JSON DO MOBILE
// Dados utilizados para cadastrar ou editar o nome de uma categoria.
public sealed class CategoriaMobileRequestDTO
{
    [Required(
        ErrorMessage =
            "Informe o nome da categoria.")]

    [StringLength(
        50,
        MinimumLength = 1,
        ErrorMessage =
            "O nome deve possuir entre " +
            "1 e 50 caracteres.")]

    public string Nome { get; set; } = "";
}

// Dados utilizados para ativar ou desativar uma categoria existente.
public sealed class CategoriaSituacaoMobileRequestDTO
{
    [Required(
        ErrorMessage =
            "Informe a situação da categoria.")]

    public bool? Ativa { get; set; }
}
