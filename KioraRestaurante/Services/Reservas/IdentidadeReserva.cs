using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.DTOs.Reserva;
using Microsoft.AspNetCore.DataProtection;
namespace KioraRestaurante.Services.Reservas;

public class IdentidadeReserva(IDataProtectionProvider provider, IWebHostEnvironment ambiente)
{
    private readonly IDataProtector _protetor=provider.CreateProtector("Kiora.Reserva.Visitante.v1");
    private const string Cookie="Kiora.Reservas";
    public string? Chave(HttpContext http,bool criar=false)
    {
        // No app, a chave é enviada em cabeçalho; nunca na URL.
        var chave=http.Request.Headers["X-Reserva-Chave"].ToString();
        if(!string.IsNullOrEmpty(chave)) return Valida(chave)?chave:null;
        if(http.Request.Cookies.TryGetValue(Cookie,out var cookie))
        {
            try { chave=_protetor.Unprotect(cookie); if(Valida(chave)) return chave; }
            catch(CryptographicException) { }
        }
        if(!criar) return null;
        chave=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        http.Response.Cookies.Append(Cookie,_protetor.Protect(chave),new CookieOptions {
            HttpOnly=true,Secure=!ambiente.IsDevelopment() || http.Request.IsHttps,SameSite=SameSiteMode.Lax,IsEssential=true,
            Expires=DateTimeOffset.UtcNow.AddDays(60),Path="/"});
        return chave;
    }
    private static bool Valida(string chave)=>chave.Length==64&&chave.All(char.IsAsciiHexDigit);
    public AcessoReserva Obter(HttpContext http,bool criar=false)
    {
        var id=int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out var usuario)?usuario:(int?)null;
        var chave=Chave(http,criar);
        return new(id,chave==null?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave))));
    }
}
