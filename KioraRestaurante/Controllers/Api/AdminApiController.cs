using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services.Admin;

namespace KioraRestaurante.Controllers.Api;

// Disponibiliza ao mobile as operações administrativas já existentes no site.
[ApiController]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Route("api/admin")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public class AdminApiController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminApiController> _logger;

    public AdminApiController(
        IAdminService adminService,
        ILogger<AdminApiController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    // Recupera o ID do administrador autenticado pelo JWT.
    private bool TentarObterAdministradorId(out int administradorId)
    {
        var idTexto = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idTexto, out administradorId);
    }

    // GET: api/admin/painel
    // Retorna os indicadores do painel administrativo.
    [HttpGet("painel")]
    public async Task<ActionResult<PainelAdminDTO>> ConsultarPainel()
    {
        if (!TentarObterAdministradorId(out var administradorId))
            return Unauthorized(new { sucesso = false, mensagem = "Não foi possível identificar o administrador." });

        try
        {
            var resultado = await _adminService.Painel(administradorId);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // GET: api/admin/usuarios
    // GET: api/admin/usuarios?busca=nome&ativo=true&pagina=1
    [HttpGet("usuarios")]
    public async Task<ActionResult<ResultadoPaginadoDTO<UsuarioAdminDTO>>> ListarUsuarios(
        [FromQuery] FiltroUsuariosAdmin filtro)
    {
        if (!TentarObterAdministradorId(out var administradorId))
            return Unauthorized(new { sucesso = false, mensagem = "Não foi possível identificar o administrador." });

        try
        {
            var resultado = await _adminService.Usuarios(administradorId, filtro);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { sucesso = false, mensagem = ex.Message });
        }
    }

    // PATCH: api/admin/usuarios/1/ativo
    // Ativa ou desativa uma conta de cliente.
    [HttpPatch("usuarios/{id:int}/ativo")]
    public async Task<IActionResult> AlterarSituacaoUsuario(
        int id,
        [FromBody] AlterarSituacaoUsuarioAdminRequestDTO dto)
    {
        if (!TentarObterAdministradorId(out var administradorId))
            return Unauthorized(new { sucesso = false, mensagem = "Não foi possível identificar o administrador." });

        if (id <= 0)
            return BadRequest(new { sucesso = false, mensagem = "Informe um ID de usuário válido." });

        if (!ModelState.IsValid || !dto.Ativo.HasValue)
            return BadRequest(new { sucesso = false, mensagem = "Informe a situação desejada para a conta." });

        try
        {
            await _adminService.AlterarSituacaoUsuario(
                administradorId,
                id,
                dto.Ativo.Value);

            return Ok(new
            {
                sucesso = true,
                mensagem = dto.Ativo.Value
                    ? "Usuário ativado com sucesso."
                    : "Usuário desativado com sucesso."
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { sucesso = false, mensagem = "Usuário não encontrado." });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { sucesso = false, mensagem = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Falha ao alterar situação do usuário {UsuarioId}.", id);
            return StatusCode(500, new
            {
                sucesso = false,
                mensagem = "Não foi possível salvar a situação da conta."
            });
        }
    }

    // GET: api/admin/pedidos
    // GET: api/admin/pedidos?status=EmPreparo&pagina=1
    [HttpGet("pedidos")]
    public async Task<ActionResult<ResultadoPaginadoDTO<PedidoAdminDTO>>> ListarPedidos(
        [FromQuery] FiltroPedidosAdmin filtro)
    {
        if (!TentarObterAdministradorId(out var administradorId))
            return Unauthorized(new { sucesso = false, mensagem = "Não foi possível identificar o administrador." });

        try
        {
            var resultado = await _adminService.Pedidos(administradorId, filtro);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { sucesso = false, mensagem = ex.Message });
        }
    }

    // GET: api/admin/pedidos/1
    // Retorna os detalhes completos de um pedido.
    [HttpGet("pedidos/{id:int}")]
    public async Task<ActionResult<PedidoDetalhesAdminDTO>> DetalhesPedido(int id)
    {
        if (!TentarObterAdministradorId(out var administradorId))
            return Unauthorized(new { sucesso = false, mensagem = "Não foi possível identificar o administrador." });

        if (id <= 0)
            return BadRequest(new { sucesso = false, mensagem = "Informe um ID de pedido válido." });

        try
        {
            var pedido = await _adminService.DetalhesPedido(administradorId, id);

            if (pedido == null)
                return NotFound(new { sucesso = false, mensagem = "Pedido não encontrado." });

            return Ok(pedido);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
