using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace KioraRestaurante.Configuracao;

public static class LimitacaoRequisicoesExtensions
{
    public static IServiceCollection AddLimitesKiora(this IServiceCollection services)
    {
        // Limita a quantidade de requisições por usuário.
        services.AddRateLimiter(limites =>
        {
            limites.AddPolicy("salao", contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? contexto.Connection.RemoteIpAddress?.ToString()
                        ?? "anonimo",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            limites.AddPolicy("convite-funcionario", contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            limites.AddPolicy("cotacao-entrega", contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? "sem-sessao",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            limites.AddPolicy("alteracoes-pedido", contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? "sem-sessao",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 6,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Consultas e gravações públicas têm limites próprios por IP, além das regras por cliente.
            limites.AddPolicy("reservas-consulta", contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            limites.AddPolicy("reservas-escrita", contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
            limites.AddPolicy("api-cadastro", contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));

            limites.OnRejected = async (contexto, ct) =>
            {
                contexto.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                if(contexto.Lease.TryGetMetadata(MetadataName.RetryAfter,out var espera))
                    contexto.HttpContext.Response.Headers.RetryAfter=Math.Max(1,(int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await contexto.HttpContext.Response.WriteAsJsonAsync(
                    new { mensagem = "Aguarde um minuto antes de tentar novamente." },
                    ct);
            };
        });


        return services;
    }
}
