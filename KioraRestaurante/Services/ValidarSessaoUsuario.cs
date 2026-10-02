using System.Security.Claims;
using KioraRestaurante.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services;

// O login já recusava inativos. Esta verificação também bloqueia cookies emitidos antes da desativação.
public class ValidarSessaoUsuario : CookieAuthenticationEvents
{
    private readonly AppDbContext _context;
    public ValidarSessaoUsuario(AppDbContext context) => _context = context;
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var valor = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var id = int.TryParse(valor, out var numero) ? numero : 0;
        var usuario = await _context.Usuarios.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Ativo, u.Tipo }).SingleOrDefaultAsync();
        if (usuario == null || !usuario.Ativo || context.Principal?.FindFirstValue(ClaimTypes.Role) != usuario.Tipo.ToString()) {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
