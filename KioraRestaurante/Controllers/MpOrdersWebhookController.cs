using KioraRestaurante.Options;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace KioraRestaurante.Controllers;
[AllowAnonymous, IgnoreAntiforgeryToken]
[Route("integracoes/mercadopago/orders")]
public class MpOrdersWebhookController(CheckoutOnlineService pagamentos,
    IOptions<MercadoPagoConfig> options, ILogger<MpOrdersWebhookController> logger,
    KioraRestaurante.Services.GerenciarPedidoService operacoes) : ControllerBase
{
    [HttpPost, RequestSizeLimit(65536)]
    public async Task<IActionResult> Receber(CancellationToken ct)
    {
        if (!options.Value.Ativado) return StatusCode(503);
        if (Request.Query["type"] != "order") return BadRequest();
        var id = Request.Query["data.id"].ToString();
        if (!AssinaturaOrders.Validar(Request.Headers["x-signature"].ToString(),
                Request.Headers["x-request-id"].ToString(), id, options.Value.WebhookSecret)) return Unauthorized();
        try { await pagamentos.Sincronizar(id, ct); await operacoes.SincronizarOrder(id, ct); return Ok(); }
        catch (Exception ex) when (ex is FalhaPagamento or DbUpdateException or FormatException
                                       or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("Notificação de order não concluída: {Tipo}.", ex.GetType().Name);
            return StatusCode(503); // Permite reenvio; não confirma algo que não foi persistido.
        }
    }
}
