
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Admin;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers.Api;

/// <summary>
/// Disponibiliza relatórios financeiros e
/// gerenciais para o Kiora Mobile.
///
/// Reutiliza os cálculos existentes no site.
/// Não modifica as regras financeiras,
/// o dashboard web ou o banco de dados.
/// </summary>
[ApiController]
[Route("api/admin/relatorios")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Authorize(
    AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public sealed class RelatoriosAdminApiController
    : ControllerBase
{
    private readonly RelatoriosAdminService _relatorios;

    public RelatoriosAdminApiController(
        RelatoriosAdminService relatorios)
    {
        _relatorios = relatorios;
    }

    // ==================================================
    // RELATÓRIO FINANCEIRO E GERENCIAL
    // ==================================================

    /// <summary>
    /// Consulta os indicadores financeiros,
    /// gráficos, produtos vendidos, cupons
    /// e histórico paginado de vendas.
    ///
    /// GET /api/admin/relatorios
    ///
    /// Filtros disponíveis:
    /// inicio
    /// fim
    /// compararInicio
    /// compararFim
    /// canal
    /// pagina
    /// tamanhoPagina
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] FiltroRelatorioAdmin filtro,
        CancellationToken ct,
        [FromQuery, Range(1, 100)]
        int tamanhoPagina = 20)
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
            // Reutiliza as regras financeiras,
            // autorização e validação de datas
            // existentes no site.

            var relatorio = await _relatorios.Obter(
                administradorId,
                filtro,
                ct);

            // ==========================================
            // PAGINAÇÃO DAS VENDAS
            // ==========================================

            var totalRegistros =
                relatorio.Vendas.Count;

            var totalPaginas =
                (int)Math.Ceiling(
                    totalRegistros /
                    (decimal)tamanhoPagina);

            // Utiliza long para evitar overflow
            // com números de página muito elevados.

            var pular =
                ((long)filtro.Pagina - 1) *
                tamanhoPagina;

            List<VendaAdminDTO> vendasPagina =
                pular >= totalRegistros
                    ? new List<VendaAdminDTO>()
                    : relatorio.Vendas
                        .Skip((int)pular)
                        .Take(tamanhoPagina)
                        .ToList();

            // ==========================================
            // COMPARAÇÃO FINANCEIRA
            // ==========================================

            var diferencaFaturamento =
                relatorio.Atual.Total -
                relatorio.Anterior.Total;

            var diferencaQuantidade =
                relatorio.Atual.Quantidade -
                relatorio.Anterior.Quantidade;

            decimal? variacaoQuantidade =
                relatorio.Anterior.Quantidade == 0
                    ? null
                    : Math.Round(
                        (
                            (decimal)diferencaQuantidade /
                            relatorio.Anterior.Quantidade
                        ) * 100m,
                        2);

            // ==========================================
            // VENDAS POR CANAL
            // ==========================================

            var quantidadeDelivery =
                relatorio.Vendas.Count(v =>
                    v.Canal == "delivery");

            var quantidadeSalao =
                relatorio.Vendas.Count(v =>
                    v.Canal == "salao");

            var ticketMedioDelivery =
                quantidadeDelivery == 0
                    ? 0m
                    : Math.Round(
                        relatorio.Atual.Delivery /
                        quantidadeDelivery,
                        2);

            var ticketMedioSalao =
                quantidadeSalao == 0
                    ? 0m
                    : Math.Round(
                        relatorio.Atual.Salao /
                        quantidadeSalao,
                        2);

            var percentualDelivery =
                relatorio.Atual.Total == 0
                    ? 0m
                    : Math.Round(
                        relatorio.Atual.Delivery /
                        relatorio.Atual.Total * 100m,
                        2);

            var percentualSalao =
                relatorio.Atual.Total == 0
                    ? 0m
                    : Math.Round(
                        relatorio.Atual.Salao /
                        relatorio.Atual.Total * 100m,
                        2);

            // ==========================================
            // DESCONTOS
            // ==========================================

            var totalDescontos =
                relatorio.Atual.DescontoCatalogo +
                relatorio.Atual.Cupons +
                relatorio.Atual.DescontosComanda;

            // ==========================================
            // RESPOSTA JSON
            // ==========================================

            return Ok(new
            {
                sucesso = true,

                geradoEmUtc =
                    relatorio.GeradoEmUtc,

                // ======================================
                // PERÍODO E FILTROS
                // ======================================

                periodo = new
                {
                    inicio = DateOnly.FromDateTime(
                        relatorio.Filtros
                            .Inicio!.Value),

                    fim = DateOnly.FromDateTime(
                        relatorio.Filtros
                            .Fim!.Value),

                    compararInicio =
                        DateOnly.FromDateTime(
                            relatorio.Filtros
                                .CompararInicio!.Value),

                    compararFim =
                        DateOnly.FromDateTime(
                            relatorio.Filtros
                                .CompararFim!.Value),

                    canal =
                        relatorio.Filtros.Canal,

                    fusoHorario =
                        "America/Sao_Paulo"
                },

                // ======================================
                // RESUMO FINANCEIRO ATUAL
                // ======================================

                resumo = new
                {
                    faturamentoTotal =
                        relatorio.Atual.Total,

                    faturamentoDelivery =
                        relatorio.Atual.Delivery,

                    faturamentoSalao =
                        relatorio.Atual.Salao,

                    produtosLiquidos =
                        relatorio.Atual.ProdutosLiquidos,

                    totalFrete =
                        relatorio.Atual.Frete,

                    quantidadeVendas =
                        relatorio.Atual.Quantidade,

                    ticketMedio = Math.Round(
                        relatorio.Atual.TicketMedio,
                        2)
                },

                // ======================================
                // COMPARAÇÃO ENTRE PERÍODOS
                // ======================================

                comparacao = new
                {
                    faturamentoAtual =
                        relatorio.Atual.Total,

                    faturamentoAnterior =
                        relatorio.Anterior.Total,

                    faturamentoDeliveryAnterior =
                        relatorio.Anterior.Delivery,

                    faturamentoSalaoAnterior =
                        relatorio.Anterior.Salao,

                    diferencaFaturamento,

                    variacaoFaturamentoPercentual =
                        relatorio.Variacao.HasValue
                            ? Math.Round(
                                relatorio.Variacao.Value,
                                2)
                            : (decimal?)null,

                    quantidadeAtual =
                        relatorio.Atual.Quantidade,

                    quantidadeAnterior =
                        relatorio.Anterior.Quantidade,

                    diferencaQuantidade,

                    variacaoQuantidadePercentual =
                        variacaoQuantidade,

                    ticketMedioAtual = Math.Round(
                        relatorio.Atual.TicketMedio,
                        2),

                    ticketMedioAnterior = Math.Round(
                        relatorio.Anterior.TicketMedio,
                        2)
                },

                // ======================================
                // DESEMPENHO DO DELIVERY
                // ======================================

                delivery = new
                {
                    faturamento =
                        relatorio.Atual.Delivery,

                    quantidadeVendas =
                        quantidadeDelivery,

                    ticketMedio =
                        ticketMedioDelivery,

                    participacaoPercentual =
                        percentualDelivery,

                    frete =
                        relatorio.Atual.Frete,

                    pedidosComCupom =
                        relatorio.Atual.PedidosComCupom,

                    descontosCupom =
                        relatorio.Atual.Cupons
                },

                // ======================================
                // DESEMPENHO DO SALÃO
                // ======================================

                salao = new
                {
                    faturamento =
                        relatorio.Atual.Salao,

                    quantidadeVendas =
                        quantidadeSalao,

                    ticketMedio =
                        ticketMedioSalao,

                    participacaoPercentual =
                        percentualSalao,

                    descontosComanda =
                        relatorio.Atual.DescontosComanda
                },

                // ======================================
                // DESCONTOS E PROMOÇÕES
                // ======================================

                descontos = new
                {
                    total =
                        totalDescontos,

                    catalogo =
                        relatorio.Atual.DescontoCatalogo,

                    cupons =
                        relatorio.Atual.Cupons,

                    comandas =
                        relatorio.Atual.DescontosComanda,

                    unidadesPromocionais =
                        relatorio.Atual.UnidadesPromocionais,

                    unidadesSemHistoricoDesconto =
                        relatorio.Atual
                            .UnidadesSemHistoricoDesconto,

                    pedidosComCupom =
                        relatorio.Atual.PedidosComCupom
                },

                // ======================================
                // GRÁFICOS
                // ======================================

                graficos = new
                {
                    vendasPorDia =
                        relatorio.Dias.Select(dia =>
                            new
                            {
                                data =
                                    DateOnly.FromDateTime(
                                        dia.Dia),

                                delivery =
                                    dia.Delivery,

                                salao =
                                    dia.Salao,

                                total =
                                    dia.Delivery +
                                    dia.Salao
                            }),

                    movimentoPorHora =
                        relatorio.Horas.Select(hora =>
                            new
                            {
                                hora =
                                    hora.Hora,

                                delivery =
                                    hora.Delivery,

                                salao =
                                    hora.Salao,

                                reservas =
                                    hora.Reservas,

                                totalVendas =
                                    hora.Delivery +
                                    hora.Salao
                            })
                },

                // ======================================
                // RANKING DE PRODUTOS
                // ======================================

                produtosMaisVendidos =
                    relatorio.Produtos,

                // ======================================
                // DESEMPENHO DOS CUPONS
                // ======================================

                cupons =
                    relatorio.Cupons,

                // ======================================
                // RESERVAS NO PERÍODO
                // ======================================

                reservas = new
                {
                    validas =
                        relatorio.ReservasPeriodo,

                    canceladasOuExpiradas =
                        relatorio.ReservasCanceladas,

                    total =
                        relatorio.ReservasPeriodo +
                        relatorio.ReservasCanceladas
                },

                // ======================================
                // HISTÓRICO DETALHADO DE VENDAS
                // ======================================

                vendas = new
                {
                    pagina =
                        filtro.Pagina,

                    tamanhoPagina,

                    totalRegistros,

                    totalPaginas,

                    temPaginaAnterior =
                        filtro.Pagina > 1 &&
                        totalPaginas > 0,

                    temProximaPagina =
                        filtro.Pagina < totalPaginas,

                    registros =
                        vendasPagina
                },

                // ======================================
                // CRITÉRIOS FINANCEIROS
                // ======================================

                criterios = new
                {
                    natureza =
                        "Relatório gerencial de vendas",

                    delivery =
                        "Considera a data do pedido e " +
                        "os critérios financeiros existentes.",

                    salao =
                        "Considera a data de fechamento " +
                        "da comanda.",

                    reservas =
                        "Considera reservas agendadas " +
                        "no período, independentemente " +
                        "do canal financeiro selecionado.",

                    conciliacaoBancaria =
                        false
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
