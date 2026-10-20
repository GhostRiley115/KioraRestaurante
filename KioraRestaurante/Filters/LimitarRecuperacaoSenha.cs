using System.Globalization;
using System.Threading.RateLimiting;
using KioraRestaurante.Data;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Filters;

public sealed class LimitarRecuperacaoSenha(LimitadorRecuperacaoSenha limitador, AppDbContext banco) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var model = context.ActionArguments.Values.FirstOrDefault(v =>
            v is EsqueciSenhaViewModel or ValidarCodigoRecuperacaoViewModel or RedefinirSenhaViewModel);
        var email = model switch
        {
            EsqueciSenhaViewModel envio => envio.Email,
            ValidarCodigoRecuperacaoViewModel codigo => codigo.Email,
            RedefinirSenhaViewModel senha => senha.Email,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 150)
        {
            await next(); // O ViewModel recusará e-mails ausentes ou grandes demais.
            return;
        }
        email = email.Trim().ToLowerInvariant();
        // A mesma conta não ganha novas tentativas por diferenças de acento ou capitalização.
        email = await banco.Usuarios.AsNoTracking().Where(u => u.Email == email)
            .Select(u => u.Email).FirstOrDefaultAsync(context.HttpContext.RequestAborted) ?? email;
        using var permissao = limitador.Adquirir(email, model is EsqueciSenhaViewModel,
            context.HttpContext.Connection.RemoteIpAddress);
        if (permissao.IsAcquired) { await next(); return; }
        var espera = permissao.TryGetMetadata(MetadataName.RetryAfter, out var restante)
            ? restante : LimitadorRecuperacaoSenha.Janela;
        var segundos = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        context.Result = new JsonResult(new
        {
            sucesso = false,
            mensagem = $"Muitas tentativas de recuperação. Aguarde {segundos} segundos antes de tentar novamente."
        }) { StatusCode = StatusCodes.Status429TooManyRequests };
    }
}
