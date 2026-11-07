using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public class AcessoSalaoService(AppDbContext db)
{
    private readonly Dictionary<int,(TipoUsuario Cargo,Guid Sessao)> _revisoes = new();
    public static bool Gestor(TipoUsuario cargo) => cargo is TipoUsuario.Administrador or TipoUsuario.Gerente;
    public static bool Funcionario(TipoUsuario cargo) => cargo is TipoUsuario.Administrador or TipoUsuario.Gerente or TipoUsuario.Garcom or TipoUsuario.Portaria or TipoUsuario.Cozinha;
    public async Task<Usuario> Exigir(int id, CancellationToken ct = default, params TipoUsuario[] cargos)
    {
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==id && u.Ativo && !u.ConvitePendente,ct);
        if (u == null || !Funcionario(u.Tipo) || u.Tipo != TipoUsuario.Administrador && !cargos.Contains(u.Tipo)) throw new UnauthorizedAccessException();
        _revisoes[id]=(u.Tipo,u.VersaoSessao);
        return u;
    }
    // Reconfere o acesso depois de adquirir a trava: uma desativação concorrente não autoriza a gravação pendente.
    public async Task ConferirAtual(int id,CancellationToken ct)
    {
        if(!_revisoes.TryGetValue(id,out var antes) || !await db.Usuarios.AnyAsync(u=>u.Id==id&&u.Ativo&&!u.ConvitePendente
            &&u.Tipo==antes.Cargo&&u.VersaoSessao==antes.Sessao,ct))throw new UnauthorizedAccessException();
    }
}
