using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace KioraRestaurante.Services;

public class CarrinhoCookie
{
    private const string Nome = "__Host-KioraCarrinho";
    private const string NomeLocal = "KioraCarrinhoDesenvolvimento";
    private readonly IDataProtector _protetor;
    private readonly IWebHostEnvironment _ambiente;

    public CarrinhoCookie(IDataProtectionProvider provider, IWebHostEnvironment ambiente)
    {
        _ambiente = ambiente;
        _protetor = provider.CreateProtector("KioraCarrinhoVisitante.v1");
    }

    // No Rider via HTTP, use outro nome: o prefixo __Host- exige HTTPS no navegador.
    // Em produção o cookie continua seguro, inclusive atrás do proxy do Render.
    private bool LocalHttp(HttpContext http) => _ambiente.IsDevelopment() && !http.Request.IsHttps;
    private string NomeCookie(HttpContext http) => LocalHttp(http) ? NomeLocal : Nome;

    public int? Ler(HttpContext httpContext)
    {
        // Pegue da requisição o cookie que tem esse nome.
        var cookie = httpContext.Request.Cookies[NomeCookie(httpContext)];

        if (string.IsNullOrWhiteSpace(cookie))
            return null;

        try
        {
            // Desprotege o Cookie para poder ler.
            var texto = _protetor.Unprotect(cookie);

            // Tenta converter o Cookie para um int e guarda na var "id".
            if (int.TryParse(texto, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                return id;
            }
        }
        catch (CryptographicException)
        {

        }
        return null;
    }

    public void Gravar(HttpContext httpContext, int carrinhoId)
    {
        // Pega o ID do carrinho e converte a uma string.
        var texto = carrinhoId.ToString(CultureInfo.InvariantCulture);

        // Protege o ID.
        var valorProtegido = _protetor.Protect(texto);

        // Joga o ID do carrinho no navegador de forma protegida para ser o Cookie.
        httpContext.Response.Cookies.Append(NomeCookie(httpContext), valorProtegido, new CookieOptions
        {
            // Impede que o JavaScript leia diretamente esse cookie.
            HttpOnly = true,
            // Faz o cookie ser enviado por HTTPS.
            Secure = !LocalHttp(httpContext),
            IsEssential = true,
            // Restringe seu envio em parte das navegações entre sites.
            SameSite = SameSiteMode.Lax,
            // Permite o uso nas rotas da aplicação.
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    public void Remover(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(NomeCookie(httpContext), new CookieOptions
        {
            HttpOnly = true,
            Secure = !LocalHttp(httpContext),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}