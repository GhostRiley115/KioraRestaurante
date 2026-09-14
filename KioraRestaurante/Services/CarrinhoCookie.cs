using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;


namespace KioraRestaurante.Services;

public class CarrinhoCookie
{
    private const string Nome = "__Host-KioraCarinho";
    private readonly IDataProtector _protetor;

    public CarrinhoCookie(IDataProtectionProvider provider)
    {
        _protetor = provider.CreateProtector("KioraCarrinhoVisitante.v1");
    }

    public int? Ler(HttpContext httpContext)
    {
        var cookie = httpContext.Request.Cookies[Nome];

        if (string.IsNullOrWhiteSpace(cookie))
            return null;

        try
        {
            var texto = _protetor.Unprotect(cookie);

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
        var texto = carrinhoId.ToString(CultureInfo.InvariantCulture);

        var valorProtegido = _protetor.Protect(texto);

        httpContext.Response.Cookies.Append(Nome, valorProtegido, new CookieOptions
        {
            //Impede que o JavaScript leia diretamente esse cookie.
            HttpOnly = true,
            //Faz o cookie ser enviado por HTTPS.
            Secure = true,
            //Restringe seu envio em parte das navegações entre sites.
            SameSite = SameSiteMode.Lax,
            //Permite o uso nas rotas da aplicação.
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    public void Remover(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(Nome, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}