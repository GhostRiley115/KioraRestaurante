using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Filters;

// Os clientes JSON recebem mensagens seguras; erros das páginas MVC seguem para a View existente.
public sealed class TratarErrosApi(ILogger<TratarErrosApi> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var caminho=http.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        if (!new PathString(caminho??http.Request.Path.Value).StartsWithSegments("/api")) return false;
        var (status, mensagem) = exception switch
        {
            RegraProdutoException => (400, exception.Message),
            ValidationException => (400, exception.Message),
            RegraReservaException => (409, exception.Message),
            KeyNotFoundException => (404, "O registro não foi encontrado."),
            UnauthorizedAccessException => (http.User.Identity?.IsAuthenticated == true ? 403 : 401, "Acesso não autorizado."),
            DbUpdateException => (409, "Não foi possível confirmar a alteração. Consulte os dados antes de repetir."),
            _ => (500, "Não foi possível concluir esta ação. Tente novamente em instantes.")
        };
        if (status == 500) logger.LogError(exception, "Falha na API. Código de suporte: {TraceId}", http.TraceIdentifier);
        http.Response.StatusCode = status;
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsJsonAsync(new { sucesso = false, mensagem, codigoSuporte = http.TraceIdentifier }, ct);
        return true;
    }
}
