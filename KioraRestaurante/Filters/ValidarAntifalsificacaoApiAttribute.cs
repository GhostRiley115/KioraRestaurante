using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KioraRestaurante.Filters;

// Bearer válido não depende de cookies; cookies e visitante continuam protegidos contra CSRF.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidarAntifalsificacaoApiAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    public int Order => 1000;
    public bool IsReusable => false;
    public IFilterMetadata CreateInstance(IServiceProvider services) =>
        new Filtro(services.GetRequiredService<IAntiforgery>());

    private sealed class Filtro(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            // O cabeçalho sozinho não dispensa proteção: assinatura, conta e sessão precisam ser válidas.
            if (http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var resultado = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (!resultado.Succeeded)
                {
                    context.Result = new UnauthorizedObjectResult(new { mensagem = "Sessão inválida ou expirada. Entre novamente." });
                    return;
                }
                return;
            }
            if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
                || HttpMethods.IsOptions(http.Request.Method) || HttpMethods.IsTrace(http.Request.Method)) return;
            try { await antiforgery.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException)
            { context.Result = new BadRequestObjectResult(new { mensagem = "Atualize a sessão antes de enviar a solicitação." }); }
        }
    }
}
