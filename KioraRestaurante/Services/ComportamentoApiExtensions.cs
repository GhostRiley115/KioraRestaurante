using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KioraRestaurante.Filters;

namespace KioraRestaurante.Services;

public static class ComportamentoApiExtensions
{
    public static IServiceCollection AddRespostasApiKiora(this IServiceCollection services)
    {
        services.AddExceptionHandler<TratarErrosApi>();
        services.Configure<ApiBehaviorOptions>(opcoes=>opcoes.InvalidModelStateResponseFactory=contexto=>
            new BadRequestObjectResult(new
            {
                sucesso=false,mensagem="Confira os campos enviados.",
                erros=contexto.ModelState.Where(c=>c.Value?.Errors.Count>0).ToDictionary(c=>c.Key,
                    c=>c.Value!.Errors.Select(e=>string.IsNullOrWhiteSpace(e.ErrorMessage)?"Valor inválido.":e.ErrorMessage).ToArray())
            }));
        return services;
    }

    // JSON inclusive em desenvolvimento; as Views continuam com seu tratamento próprio.
    public static IApplicationBuilder UseRespostasApiKiora(this IApplicationBuilder app) => app.UseWhen(
        http => http.Request.Path.StartsWithSegments("/api"), api =>
        {
            api.UseExceptionHandler("/Home/Error");
            api.UseStatusCodePages(async estado =>
            {
                var http=estado.HttpContext;
                var mensagem=http.Response.StatusCode switch
                {
                    400=>"Confira os dados enviados.",401=>"Entre na conta para continuar.",
                    403=>"Sua conta não tem permissão para esta ação.",404=>"O registro não foi encontrado.",
                    405=>"O método utilizado não é permitido nesta rota.",429=>"Aguarde antes de tentar novamente.",
                    _=>"Não foi possível concluir esta ação."
                };
                http.Response.Headers.CacheControl="no-store";
                await http.Response.WriteAsJsonAsync(new{sucesso=false,mensagem});
            });
        });
}
