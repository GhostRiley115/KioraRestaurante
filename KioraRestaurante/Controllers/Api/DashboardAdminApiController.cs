using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.Data;
using KioraRestaurante.Helpers;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services.Admin;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

/// <summary>
/// Dashboard administrativo consolidado
/// para o Kiora Mobile.
///
/// Reúne indicadores financeiros e operacionais
/// do salão e do delivery.
///
/// Não modifica o dashboard web existente.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Authorize(
    AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public sealed class DashboardAdminApiController
    : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAdminService _adminService;
    private readonly RelatoriosAdminService _relatorios;
    private readonly ConsultaSalaoService _salao;

    public DashboardAdminApiController(
        AppDbContext context,
        IAdminService adminService,
        RelatoriosAdminService relatorios,
        ConsultaSalaoService salao)
    {
        _context = context;
        _adminService = adminService;
        _relatorios = relatorios;
        _salao = salao;
    }

    // ==================================================
    // DASHBOARD CONSOLIDADO
    // ==================================================

    /// <summary>
    /// Retorna os indicadores administrativos.
    ///
    /// Exemplo:
    /// GET /api/admin/dashboard
    ///
    /// Com período personalizado:
    /// GET /api/admin/dashboard
    ///     ?inicio=2026-11-01
    ///     &fim=2026-11-21
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] FiltroRelatorioAdmin filtro,
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

        // O dashboard consolidado sempre
        // considera os dois canais de venda.
        if (filtro.Canal != "todos")
        {
            return BadRequest(new
            {
                sucesso = false,
                mensagem =
                    "O dashboard consolidado utiliza " +
                    "os canais salão e delivery."
            });
        }

        try
        {
            // Reutiliza a autorização e os
            // contadores administrativos existentes.
            var painel = await _adminService
                .Painel(administradorId);

            // Reutiliza as regras financeiras
            // já utilizadas pelo painel web.
            var relatorio = await _relatorios.Obter(
                administradorId,
                filtro,
                ct);

            // Recupera as situações reais das mesas.
            var mesas = await _salao.Mesas(
                administradorId,
                ct);

            var inicio = HorarioRestaurante
                .InicioDiaUtc(
                    relatorio.Filtros.Inicio!.Value);

            var fim = HorarioRestaurante
                .InicioDiaUtc(
                    relatorio.Filtros.Fim!.Value
                        .AddDays(1));

            // ==========================================
            // USUÁRIOS
            // ==========================================

            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .GroupBy(u => new
                {
                    u.Tipo,
                    u.Ativo,
                    u.ConvitePendente
                })
                .Select(grupo => new
                {
                    grupo.Key.Tipo,
                    grupo.Key.Ativo,
                    grupo.Key.ConvitePendente,
                    Quantidade = grupo.Count()
                })
                .ToListAsync(ct);

            var clientesAtivos = usuarios
                .Where(u =>
                    u.Tipo == TipoUsuario.Cliente &&
                    u.Ativo)
                .Sum(u => u.Quantidade);

            var clientesInativos = usuarios
                .Where(u =>
                    u.Tipo == TipoUsuario.Cliente &&
                    !u.Ativo)
                .Sum(u => u.Quantidade);

            var funcionariosAtivos = usuarios
                .Where(u =>
                    u.Tipo != TipoUsuario.Cliente &&
                    u.Ativo &&
                    !u.ConvitePendente)
                .Sum(u => u.Quantidade);

            var funcionariosInativos = usuarios
                .Where(u =>
                    u.Tipo != TipoUsuario.Cliente &&
                    !u.Ativo)
                .Sum(u => u.Quantidade);

            // ==========================================
            // RESERVAS AGENDADAS DO PERÍODO
            // ==========================================

            var reservas = await _context.Reservas
                .AsNoTracking()
                .Where(r =>
                    r.Tipo == TipoReserva.Agendada &&
                    r.InicioUtc >= inicio &&
                    r.InicioUtc < fim)
                .GroupBy(r => r.Situacao)
                .Select(grupo => new
                {
                    Situacao = grupo.Key,
                    Quantidade = grupo.Count()
                })
                .ToListAsync(ct);

            int ContarReservas(SituacaoReserva situacao)
            {
                return reservas
                    .Where(r =>
                        r.Situacao == situacao)
                    .Sum(r => r.Quantidade);
            }

            var reservasAguardandoChegada =
                ContarReservas(
                    SituacaoReserva.Confirmada) +
                ContarReservas(
                    SituacaoReserva.AguardandoChegada);

            // ==========================================
            // COMANDAS
            // ==========================================

            var comandasAbertas =
                await _context.AtendimentosMesa
                    .AsNoTracking()
                    .CountAsync(
                        a => a.Situacao ==
                            SituacaoAtendimento.Aberto,
                        ct);

            var comandasEncerradas =
                await _context.AtendimentosMesa
                    .AsNoTracking()
                    .CountAsync(
                        a =>
                            a.FechadoEmUtc >= inicio &&
                            a.FechadoEmUtc < fim &&
                            (
                                a.Situacao ==
                                    SituacaoAtendimento
                                        .AguardandoLimpeza ||
                                a.Situacao ==
                                    SituacaoAtendimento
                                        .Encerrado
                            ),
                        ct);

            // ==========================================
            // PRODUTOS MAIS VENDIDOS
            // ==========================================

            var produtosMaisVendidos =
                relatorio.Produtos
                    .Take(10)
                    .ToList();

            // ==========================================
            // VENDAS POR CANAL
            // ==========================================

            var vendasDelivery = relatorio.Vendas
                .Count(v => v.Canal == "delivery");

            var vendasSalao = relatorio.Vendas
                .Count(v => v.Canal == "salao");

            var ticketMedioDelivery =
                vendasDelivery == 0
                    ? 0m
                    : relatorio.Atual.Delivery /
                        vendasDelivery;

            var ticketMedioSalao =
                vendasSalao == 0
                    ? 0m
                    : relatorio.Atual.Salao /
                        vendasSalao;

            // ==========================================
            // RESPOSTA CONSOLIDADA PARA O MOBILE
            // ==========================================

            return Ok(new
            {
                sucesso = true,

                geradoEmUtc =
                    relatorio.GeradoEmUtc,

                periodo = new
                {
                    inicio = DateOnly.FromDateTime(
                        relatorio.Filtros.Inicio!.Value),

                    fim = DateOnly.FromDateTime(
                        relatorio.Filtros.Fim!.Value),

                    compararInicio =
                        DateOnly.FromDateTime(
                            relatorio.Filtros
                                .CompararInicio!.Value),

                    compararFim =
                        DateOnly.FromDateTime(
                            relatorio.Filtros
                                .CompararFim!.Value),

                    fusoHorario = "America/Sao_Paulo"
                },

                financeiro = new
                {
                    faturamentoTotal =
                        relatorio.Atual.Total,

                    faturamentoDelivery =
                        relatorio.Atual.Delivery,

                    faturamentoSalao =
                        relatorio.Atual.Salao,

                    totalFrete =
                        relatorio.Atual.Frete,

                    produtosLiquidos =
                        relatorio.Atual.ProdutosLiquidos,

                    quantidadeVendas =
                        relatorio.Atual.Quantidade,

                    quantidadeVendasDelivery =
                        vendasDelivery,

                    quantidadeVendasSalao =
                        vendasSalao,

                    ticketMedio = Math.Round(
                        relatorio.Atual.TicketMedio, 2),

                    ticketMedioDelivery =
                        Math.Round(ticketMedioDelivery, 2),

                    ticketMedioSalao =
                        Math.Round(ticketMedioSalao, 2),

                    faturamentoAnterior =
                        relatorio.Anterior.Total,

                    variacaoPercentual =
                        relatorio.Variacao.HasValue
                            ? Math.Round(
                                relatorio.Variacao.Value, 2)
                            : (decimal?)null,

                    revisoesFinanceiras =
                        relatorio.RevisoesFinanceiras
                },

                descontos = new
                {
                    catalogo =
                        relatorio.Atual.DescontoCatalogo,

                    cupons =
                        relatorio.Atual.Cupons,

                    comandas =
                        relatorio.Atual.DescontosComanda,

                    pedidosComCupom =
                        relatorio.Atual.PedidosComCupom,

                    unidadesPromocionais =
                        relatorio.Atual.UnidadesPromocionais,

                    unidadesSemHistoricoDesconto =
                        relatorio.Atual
                            .UnidadesSemHistoricoDesconto
                },

                pedidos = new
                {
                    totalHistorico =
                        painel.TotalPedidos,

                    emAndamento =
                        painel.PedidosEmAndamento,

                    naFila =
                        relatorio.DeliveryNaFila,

                    emPreparo =
                        relatorio.DeliveryEmPreparo,

                    emEntrega =
                        relatorio.DeliveryEmEntrega,

                    aguardandoPagamento =
                        relatorio.DeliveryAguardandoPagamento
                },

                salao = new
                {
                    totalMesas = mesas.Count,

                    mesasLivres = mesas.Count(m =>
                        m.Situacao == "Livre"),

                    mesasOcupadas = mesas.Count(m =>
                        m.Situacao == "Ocupada"),

                    mesasAguardandoLimpeza =
                        mesas.Count(m =>
                            m.Situacao ==
                                "Aguardando limpeza"),

                    mesasAguardandoGarcom =
                        mesas.Count(m =>
                            m.Situacao ==
                                "Aguardando garçom"),

                    mesasIndisponiveis =
                        mesas.Count(m =>
                            m.Situacao == "Indisponível"),

                    comandasAbertas,

                    comandasEncerradas,

                    gruposNaFila =
                        relatorio.GruposNaFila
                },

                reservas = new
                {
                    total = reservas.Sum(
                        r => r.Quantidade),

                    aguardandoChegada =
                        reservasAguardandoChegada,

                    aguardandoNaFila =
                        ContarReservas(
                            SituacaoReserva.Aguardando),

                    chamadas =
                        ContarReservas(
                            SituacaoReserva.Chamada),

                    emAtendimento =
                        ContarReservas(
                            SituacaoReserva.EmAtendimento),

                    concluidas =
                        ContarReservas(
                            SituacaoReserva.Concluida),

                    canceladas =
                        ContarReservas(
                            SituacaoReserva.Cancelada),

                    expiradas =
                        ContarReservas(
                            SituacaoReserva.Expirada)
                },

                usuarios = new
                {
                    totalAtivos =
                        painel.UsuariosAtivos,

                    totalInativos =
                        painel.UsuariosInativos,

                    clientesAtivos,

                    clientesInativos,

                    funcionariosAtivos,

                    funcionariosInativos,

                    convitesPendentes = usuarios
                        .Where(u =>
                            u.Tipo != TipoUsuario.Cliente &&
                            u.ConvitePendente)
                        .Sum(u => u.Quantidade)
                },

                graficos = new
                {
                    vendasPorDia =
                        relatorio.Dias,

                    movimentoPorHora =
                        relatorio.Horas,

                    produtosMaisVendidos
                }
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValidationException erro)
        {
            return BadRequest(new
            {
                sucesso = false,
                mensagem = erro.Message
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
}
