using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Pedidos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

/// <summary>
/// Operações administrativas dos pedidos
/// delivery para o Kiora Mobile.
///
/// Reutiliza as regras de negócio existentes
/// no painel administrativo do site.
/// </summary>
[ApiController]
[Route("api/admin/pedidos")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Authorize(
    AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public sealed class PedidosAdminApiController
    : ControllerBase
{
    private readonly AndamentoPedidoService _andamento;

    private readonly GerenciarPedidoService _gerenciamento;

    private readonly ILogger<PedidosAdminApiController> _logger;

    public PedidosAdminApiController(
        AndamentoPedidoService andamento,
        GerenciarPedidoService gerenciamento,
        ILogger<PedidosAdminApiController> logger)
    {
        _andamento = andamento;
        _gerenciamento = gerenciamento;
        _logger = logger;
    }

    // ==================================================
    // AVANÇAR A SITUAÇÃO DO PEDIDO
    // ==================================================

    /// <summary>
    /// Avança o pedido para sua próxima
    /// etapa operacional.
    ///
    /// POST /api/admin/pedidos/{id}/avancar
    /// </summary>
    [HttpPost("{id:int}/avancar")]
    public async Task<IActionResult> Avancar(
        int id,
        [FromBody] AvancarPedidoMobileRequestDTO dto,
        CancellationToken ct)
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

        if (id <= 0)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe um ID de pedido válido."
            });
        }

        if (!dto.Versao.HasValue ||
            dto.Versao.Value == Guid.Empty)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe a versão atual do pedido."
            });
        }

        if (!dto.Proximo.HasValue ||
            !Enum.IsDefined(dto.Proximo.Value) ||
            dto.Proximo.Value is
                < StatusPedido.EmPreparo or
                > StatusPedido.Entregue)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe uma próxima etapa válida."
            });
        }

        try
        {
            // O serviço verifica:
            //
            // - Administrador ativo.
            // - Existência do pedido.
            // - Versão atual.
            // - Operações pendentes.
            // - Transição de situação.
            // - Condições de pagamento.
            // - Situação dos itens na cozinha.

            await _andamento.Avancar(
                administradorId,
                id,
                dto.Versao.Value,
                dto.Proximo.Value,
                ct);

            return Ok(new
            {
                sucesso = true,

                mensagem =
                    "Situação do pedido atualizada " +
                    "com sucesso.",

                pedidoId = id,

                situacao = (int)dto.Proximo.Value,

                situacaoTexto =
                    TextoSituacao(dto.Proximo.Value),

                atualizarDetalhes = true
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

                mensagem =
                    "Pedido não encontrado."
            });
        }
        catch (ValidationException erro)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem = erro.Message,

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "O pedido mudou durante a atualização. " +
                    "Consulte novamente os detalhes.",

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateException erro)
        {
            _logger.LogError(
                "Falha ao atualizar o pedido {PedidoId}. " +
                "Tipo: {TipoErro}.",
                id,
                erro.GetType().Name);

            return StatusCode(500, new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível confirmar " +
                    "a atualização do pedido.",

                atualizarDetalhes = true
            });
        }
    }

    // ==================================================
    // CANCELAR PEDIDO ADMINISTRATIVAMENTE
    // ==================================================

    /// <summary>
    /// Solicita o cancelamento do pedido,
    /// respeitando as regras financeiras existentes.
    ///
    /// POST /api/admin/pedidos/{id}/cancelar
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [EnableRateLimiting("alteracoes-pedido")]
    public async Task<IActionResult> Cancelar(
        int id,
        [FromBody] CancelarPedidoMobileRequestDTO dto,
        CancellationToken ct)
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

        if (id <= 0)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe um ID de pedido válido."
            });
        }

        if (!dto.VersaoPedido.HasValue ||
            dto.VersaoPedido.Value == Guid.Empty)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe a versão atual do pedido."
            });
        }

        var motivo = dto.Motivo.Trim();

        if (string.IsNullOrWhiteSpace(motivo) ||
            motivo.Length > 300)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Informe um motivo válido, " +
                    "com até 300 caracteres."
            });
        }

        // Converte o contrato mobile para o
        // DTO administrativo já utilizado pelo site.

        var solicitacao =
            new CancelarPedidoAdminRequestDTO
            {
                PedidoId = id,

                VersaoPedido =
                    dto.VersaoPedido.Value,

                Motivo = motivo
            };

        try
        {
            // Não altera diretamente a situação
            // do pagamento ou do pedido.
            //
            // O serviço existente registra,
            // processa e confere o cancelamento.

            var resultado =
                await _gerenciamento
                    .CancelarPeloAdministrador(
                        administradorId,
                        solicitacao,
                        ct);

            // Uma solicitação em conferência
            // NÃO equivale a um pedido cancelado.

            if (!resultado.Concluido)
            {
                return StatusCode(202, new
                {
                    sucesso = false,

                    concluido = false,

                    emConferencia = true,

                    pedidoId = id,

                    reembolsoPendente =
                        resultado.ReembolsoPendente,

                    mensagem =
                        "O cancelamento está em conferência. " +
                        "O pedido ainda não foi cancelado. " +
                        "Atualize os detalhes para acompanhar.",

                    atualizarDetalhes = true
                });
            }

            // O serviço confirmou que o pedido
            // está efetivamente cancelado.

            return Ok(new
            {
                sucesso = true,

                concluido = true,

                emConferencia = false,

                pedidoId = id,

                situacao =
                    (int)StatusPedido.Cancelado,

                situacaoTexto =
                    "Cancelado",

                reembolsoPendente =
                    resultado.ReembolsoPendente,

                mensagem =
                    resultado.ReembolsoPendente
                        ? "Pedido cancelado. Confira a " +
                          "devolução do valor recebido, " +
                          "incluindo eventuais ajustes " +
                          "de entrega. Nenhum estorno " +
                          "automático foi confirmado."
                        : "Pedido cancelado com sucesso.",

                atualizarDetalhes = true
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

                mensagem =
                    "Pedido não encontrado."
            });
        }
        catch (ValidationException erro)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem = erro.Message,

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "O pedido mudou durante o cancelamento. " +
                    "Confira a situação atual.",

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateException erro)
        {
            _logger.LogError(
                "Falha ao cancelar o pedido {PedidoId}. " +
                "Tipo: {TipoErro}.",
                id,
                erro.GetType().Name);

            return StatusCode(500, new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível confirmar o " +
                    "cancelamento. Atualize os detalhes " +
                    "antes de repetir a solicitação.",

                atualizarDetalhes = true
            });
        }
    }

    // ==================================================
    // IDENTIFICAÇÃO DO ADMINISTRADOR
    // ==================================================

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

    // ==================================================
    // DESCRIÇÃO DAS SITUAÇÕES
    // ==================================================

    private static string TextoSituacao(
        StatusPedido situacao)
    {
        return situacao switch
        {
            StatusPedido.Recebido =>
                "Recebido",

            StatusPedido.EmPreparo =>
                "Em preparo",

            StatusPedido.SaiuParaEntrega =>
                "Saiu para entrega",

            StatusPedido.Entregue =>
                "Entregue",

            StatusPedido.Cancelado =>
                "Cancelado",

            _ =>
                "Situação desconhecida"
        };
    }
}

// ======================================================
// CONTRATOS JSON EXCLUSIVOS DO MOBILE
// ======================================================

/// <summary>
/// Solicitação para avançar a situação
/// de um pedido delivery.
/// </summary>
public sealed class AvancarPedidoMobileRequestDTO
{
    [Required(
        ErrorMessage =
            "Informe a versão do pedido.")]
    public Guid? Versao { get; set; }

    [Required(
        ErrorMessage =
            "Informe a próxima etapa.")]
    public StatusPedido? Proximo { get; set; }
}

/// <summary>
/// Solicitação de cancelamento
/// administrativo pelo Mobile.
/// </summary>
public sealed class CancelarPedidoMobileRequestDTO
{
    [Required(
        ErrorMessage =
            "Informe a versão do pedido.")]
    public Guid? VersaoPedido { get; set; }

    [Required(
        ErrorMessage =
            "Informe o motivo do cancelamento.")]
    [StringLength(
        300,
        ErrorMessage =
            "Use até 300 caracteres no motivo.")]
    public string Motivo { get; set; } = "";
}
