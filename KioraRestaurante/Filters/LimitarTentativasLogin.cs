using System.Globalization;
using System.Threading.RateLimiting;
using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.ViewModels;
using KioraRestaurante.DTOs.Usuario;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KioraRestaurante.Services.Autenticacao;
using KioraRestaurante.ViewModels.Conta;

namespace KioraRestaurante.Filters;

// Executa após a leitura do formulário e antes de consultar a senha no banco.
public sealed class LimitarTentativasLogin(LimitadorLogin limitador, AppDbContext banco) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var model = context.ActionArguments.Values.OfType<LoginViewModel>().FirstOrDefault();
        // Formulário web e JSON do aplicativo usam a mesma partição por conta.
        var emailInformado = model?.Email ?? context.ActionArguments.Values.OfType<UsuarioLoginDTO>().FirstOrDefault()?.Email;
        var email = emailInformado?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(email) && email.Length <= LimitadorLogin.TamanhoMaximoEmail)
        {
            // A comparação usa a mesma collation do login: acentos e outras variantes
            // reconhecidas pelo MySQL devem consumir o limite da mesma conta.
            email = await banco.Usuarios.AsNoTracking().Where(u => u.Email == email)
                .Select(u => u.Email).FirstOrDefaultAsync(context.HttpContext.RequestAborted) ?? email;
        }
        using var permissao = limitador.AdquirirPermissao(
            context.HttpContext.Connection.RemoteIpAddress, email);

        if (!permissao.IsAcquired)
        {
            var espera = permissao.TryGetMetadata(MetadataName.RetryAfter, out var restante)
                ? restante
                : LimitadorLogin.Janela;
            var segundos = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
            context.Result = new JsonResult(new
            {
                sucesso = false,
                mensagem = $"Muitas tentativas de login. Aguarde {segundos} segundos e tente novamente."
            })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        // O limite é o mesmo do e-mail armazenado no banco e impede chaves arbitrariamente grandes.
        if (emailInformado.AsSpan().Trim().Length > LimitadorLogin.TamanhoMaximoEmail)
        {
            context.Result = new BadRequestObjectResult(new
            {
                sucesso = false,
                mensagem = $"O e-mail deve ter no máximo {LimitadorLogin.TamanhoMaximoEmail} caracteres."
            });
            return;
        }

        await next();
    }
}
