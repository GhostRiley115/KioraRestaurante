using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public class FuncionarioService(AppDbContext db,AcessoSalaoService acesso,EmailService email,IConfiguration config,TimeProvider relogio)
{
    private DateTime Agora=>relogio.GetUtcNow().UtcDateTime;
    private static readonly TipoUsuario[] Cargos=[TipoUsuario.Garcom,TipoUsuario.Portaria,TipoUsuario.Cozinha,TipoUsuario.Gerente];
    private async Task<Usuario> Gestor(int id,CancellationToken ct)=>await acesso.Exigir(id,ct,TipoUsuario.Gerente);
    private static void Permissao(Usuario gestor,TipoUsuario cargo)
    {if(!Cargos.Contains(cargo)||gestor.Tipo==TipoUsuario.Gerente&&cargo==TipoUsuario.Gerente)throw new UnauthorizedAccessException();}
    public async Task<List<FuncionarioDTO>> Listar(int id,CancellationToken ct=default)
    {
        await Gestor(id,ct);
        return await db.Usuarios.AsNoTracking().Where(u=>u.Tipo==TipoUsuario.Garcom||u.Tipo==TipoUsuario.Portaria||u.Tipo==TipoUsuario.Cozinha||u.Tipo==TipoUsuario.Gerente)
            .OrderBy(u=>u.Nome).Select(u=>new FuncionarioDTO(u.Id,u.Nome,u.Email,u.TelefoneFuncionario,u.Tipo,u.Ativo,u.ConvitePendente,u.VersaoFuncionario)).ToListAsync(ct);
    }
    public async Task<SalvarFuncionarioDTO> Edicao(int id,int funcionarioId,CancellationToken ct=default)
    {
        var gestor=await Gestor(id,ct);var u=await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==funcionarioId,ct)??throw new KeyNotFoundException();
        Permissao(gestor,u.Tipo);
        return new(){Id=u.Id,Nome=u.Nome,Email=u.Email,Telefone=u.TelefoneFuncionario,Cargo=u.Tipo,Ativo=u.Ativo,Versao=u.VersaoFuncionario};
    }
    public async Task<ResultadoFuncionario> Salvar(int id,SalvarFuncionarioDTO dto,CancellationToken ct=default)
    {
        var gestor=await Gestor(id,ct);Validator.ValidateObject(dto,new(dto),true);Permissao(gestor,dto.Cargo);
        var emailNormalizado=dto.Email.Trim().ToLowerInvariant();
        var telefone=new string((dto.Telefone??"").Where(char.IsAsciiDigit).ToArray());
        if(telefone.Length is not (0 or 10 or 11))throw new ValidationException("Confira o telefone com DDD.");
        string? token=null;Usuario usuario;
        await using(var tx=await TransacaoSalao.Iniciar(db,ct))
        {
            await acesso.ConferirAtual(id,ct);
            try
            {
                if(await db.Usuarios.AnyAsync(u=>u.Email==emailNormalizado&&u.Id!=dto.Id,ct))throw new ValidationException("Este e-mail já pertence a outra conta. Use um e-mail próprio do funcionário.");
                if(dto.Id.HasValue)
                {
                    usuario=await db.Usuarios.SingleOrDefaultAsync(u=>u.Id==dto.Id,ct)??throw new KeyNotFoundException();Permissao(gestor,usuario.Tipo);
                    if(usuario.Id==id)throw new ValidationException("Não altere seu próprio cargo ou acesso por este formulário.");
                    if(usuario.VersaoFuncionario!=dto.Versao)throw new ValidationException("O cadastro mudou. Abra a edição novamente.");
                    if((!dto.Ativo||dto.Cargo!=usuario.Tipo)&&await db.AtendimentosMesa.AnyAsync(a=>a.GarcomId==usuario.Id&&a.MesaOcupadaId!=null,ct))throw new ValidationException("Transfira e libere as mesas desse funcionário antes de desativá-lo ou trocar o cargo.");
                    if(usuario.Email!=emailNormalizado){usuario.ConvitePendente=true;usuario.SenhaHash=BloquearSenha(usuario);}
                }
                else
                {
                    usuario=new(){Nome=dto.Nome.Trim(),Email=emailNormalizado,Tipo=dto.Cargo,ConvitePendente=true};usuario.SenhaHash=BloquearSenha(usuario);db.Usuarios.Add(usuario);
                }
                usuario.Nome=dto.Nome.Trim();usuario.Email=emailNormalizado;usuario.Tipo=dto.Cargo;usuario.Ativo=dto.Ativo;
                usuario.TelefoneFuncionario=telefone.Length==0?null:telefone;usuario.VersaoSessao=Guid.NewGuid();usuario.VersaoFuncionario=Guid.NewGuid();
                usuario.TokenRecuperacaoSenha=null;usuario.ExpiracaoTokenRecuperacaoSenha=null;
                if(usuario.ConvitePendente&&usuario.Ativo)token=GerarConvite(usuario,id);
                await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            }
            catch{await tx.RollbackAsync(ct);db.ChangeTracker.Clear();throw;}
        }
        return new(usuario.Id,token==null||await Enviar(usuario.Email,token,ct));
    }
    public async Task<bool> Reenviar(int id,int funcionarioId,Guid versao,CancellationToken ct=default)
    {
        var gestor=await Gestor(id,ct);Usuario usuario;string token;
        await using(var tx=await TransacaoSalao.Iniciar(db,ct))
        {
            await acesso.ConferirAtual(id,ct);
            usuario=await db.Usuarios.SingleOrDefaultAsync(u=>u.Id==funcionarioId,ct)??throw new KeyNotFoundException();Permissao(gestor,usuario.Tipo);
            if(!usuario.Ativo||!usuario.ConvitePendente||usuario.VersaoFuncionario!=versao)throw new ValidationException("Confira se o funcionário está ativo e ainda aguarda o convite.");
            usuario.VersaoSessao=Guid.NewGuid();usuario.VersaoFuncionario=Guid.NewGuid();token=GerarConvite(usuario,id);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        return await Enviar(usuario.Email,token,ct);
    }
    private static string BloquearSenha(Usuario u)=>new PasswordHasher<Usuario>().HashPassword(u,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    private string GerarConvite(Usuario usuario,int autor)
    {
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.ConvitesFuncionarios.Add(new(){Usuario=usuario,CriadoPorId=autor,TokenHash=Hash(token),VersaoSessao=usuario.VersaoSessao,CriadoEmUtc=Agora,ExpiraEmUtc=Agora.AddHours(24)});
        return token;
    }
    private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task<bool> Enviar(string destino,string token,CancellationToken ct)
    {
        var origem=(config["Aplicacao:UrlPublica"]??config["MercadoPago:UrlPublica"]??"").TrimEnd('/');
        if(!Uri.TryCreate(origem,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))return false;
        return await email.EnviarConviteFuncionarioAsync(destino,origem+"/Funcionarios/Convite?token="+Uri.EscapeDataString(token),ct);
    }
    public async Task<bool> ConferirConvite(string? token,CancellationToken ct=default)
    {
        if(token==null||token.Length!=64||!token.All(Uri.IsHexDigit))return false;
        var hash=Hash(token);
        return await db.ConvitesFuncionarios.AnyAsync(c=>c.TokenHash==hash&&c.UsadoEmUtc==null&&c.ExpiraEmUtc>Agora&&c.Usuario.Ativo&&c.Usuario.ConvitePendente
            &&c.VersaoSessao==c.Usuario.VersaoSessao,ct);
    }
    public async Task Aceitar(AceitarConviteDTO dto,CancellationToken ct=default)
    {
        Validator.ValidateObject(dto,new(dto),true);
        if(dto.Token.Length!=64||!dto.Token.All(Uri.IsHexDigit))throw new ValidationException("Convite inválido ou expirado. Peça um novo convite ao gerente.");
        var hash=Hash(dto.Token);await using var tx=await TransacaoSalao.Iniciar(db,ct);
        try
        {
            var convite=await db.ConvitesFuncionarios.Include(c=>c.Usuario).SingleOrDefaultAsync(c=>c.TokenHash==hash,ct);
            if(convite==null||convite.UsadoEmUtc!=null||convite.ExpiraEmUtc<=Agora||!convite.Usuario.Ativo||!convite.Usuario.ConvitePendente||convite.VersaoSessao!=convite.Usuario.VersaoSessao)
                throw new ValidationException("Convite inválido ou expirado. Peça um novo convite ao gerente.");
            var usuario=convite.Usuario;usuario.SenhaHash=new PasswordHasher<Usuario>().HashPassword(usuario,dto.Senha);
            usuario.ConvitePendente=false;usuario.VersaoSessao=Guid.NewGuid();usuario.VersaoFuncionario=Guid.NewGuid();convite.UsadoEmUtc=Agora;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        }
        catch{await tx.RollbackAsync(ct);db.ChangeTracker.Clear();throw;}
    }
}
