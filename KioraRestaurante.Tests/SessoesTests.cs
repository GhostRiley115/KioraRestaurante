using System.Security.Claims;
using KioraRestaurante.Controllers;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

public class SessoesTests
{
    private const string SenhaOriginal = "SenhaOriginal123!";
    private const string NovaSenha = "NovaSenha456!";

    [Fact]
    public void CadaUsuarioRecebeUmaVersaoPropria()
    {
        var primeiro = new Usuario();
        var segundo = new Usuario();
        Assert.NotEqual(Guid.Empty, primeiro.VersaoSessao);
        Assert.NotEqual(primeiro.VersaoSessao, segundo.VersaoSessao);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalida")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task CookieSemVersaoValidaERejeitado(string? versao)
    {
        using var banco = new BancoSessoes();
        var cookie = await banco.CriarCookie(versao);
        Assert.False((await banco.AutenticarCookie(cookie)).Succeeded);
    }

    [Fact]
    public async Task TrocaDeSenhaRevogaCookiesAntigosEMantemApenasAVersaoNova()
    {
        using var banco = new BancoSessoes();
        var cookieA = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        var cookieB = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        Assert.True((await banco.AutenticarCookie(cookieA)).Succeeded);
        using var contexto = banco.CriarContexto();
        var servico = new UsuarioService(contexto);
        var token = servico.GerarTokenRecuperacao(new() { Email = banco.Usuario.Email });
        Assert.NotNull(token);
        var novaVersao = servico.AlterarSenha(banco.Usuario.Id, Alteracao(), banco.Usuario.VersaoSessao);
        Assert.NotNull(novaVersao);
        Assert.NotEqual(banco.Usuario.VersaoSessao, novaVersao);
        Assert.False((await banco.AutenticarCookie(cookieA)).Succeeded);
        Assert.False((await banco.AutenticarCookie(cookieB)).Succeeded);
        var cookieNovo = await banco.CriarCookie(novaVersao.ToString());
        Assert.True((await banco.AutenticarCookie(cookieNovo)).Succeeded);
        Assert.Null(servico.Autenticar(new() { Email = banco.Usuario.Email, Senha = SenhaOriginal }));
        Assert.NotNull(servico.Autenticar(new() { Email = banco.Usuario.Email, Senha = NovaSenha }));
        Assert.Null(contexto.Usuarios.AsNoTracking().Single(u => u.Id == banco.Usuario.Id).TokenRecuperacaoSenha);
    }

    [Fact]
    public async Task SenhaIncorretaNaoAlteraAVersaoNemEncerraSessoes()
    {
        using var banco = new BancoSessoes();
        var cookie = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        using var contexto = banco.CriarContexto();
        var dto = Alteracao();
        dto.SenhaAtual = "incorreta";
        Assert.Null(new UsuarioService(contexto).AlterarSenha(banco.Usuario.Id, dto, banco.Usuario.VersaoSessao));
        Assert.Equal(banco.Usuario.VersaoSessao, contexto.Usuarios.AsNoTracking().Single(u => u.Id == banco.Usuario.Id).VersaoSessao);
        Assert.True((await banco.AutenticarCookie(cookie)).Succeeded);
    }

    [Fact]
    public void VersaoRevogadaNaoPodeAlterarSenhaMesmoConhecendoASenhaAtual()
    {
        using var banco = new BancoSessoes();
        using var contexto = banco.CriarContexto();
        var servico = new UsuarioService(contexto);
        var novaVersao = servico.AlterarSenha(banco.Usuario.Id, Alteracao(), banco.Usuario.VersaoSessao);
        var dto = Alteracao();
        dto.SenhaAtual = NovaSenha;
        dto.NovaSenha = dto.ConfirmarNovaSenha = "OutraSenha789!";
        Assert.Null(servico.AlterarSenha(banco.Usuario.Id, dto, banco.Usuario.VersaoSessao));
        Assert.Equal(novaVersao, contexto.Usuarios.AsNoTracking().Single(u => u.Id == banco.Usuario.Id).VersaoSessao);
    }

    [Fact]
    public async Task RedefinicaoRevogaSessoesEImpedeReutilizacaoDoToken()
    {
        using var banco = new BancoSessoes();
        var cookie = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        using var contexto = banco.CriarContexto();
        var servico = new UsuarioService(contexto);
        var token = servico.GerarTokenRecuperacao(new() { Email = banco.Usuario.Email });
        var dto = new UsuarioRedefinirSenhaDTO { Email = banco.Usuario.Email, Token = token!.Codigo, NovaSenha = NovaSenha, ConfirmarNovaSenha = NovaSenha };
        Assert.True(servico.RedefinirSenha(dto));
        Assert.False(servico.RedefinirSenha(dto));
        Assert.False((await banco.AutenticarCookie(cookie)).Succeeded);
        Assert.NotNull(servico.Autenticar(new() { Email = banco.Usuario.Email, Senha = NovaSenha }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReativarContaNaoRestauraCookiesAntigos(bool desativarPeloProprioUsuario)
    {
        using var banco = new BancoSessoes();
        var cookie = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        using var contexto = banco.CriarContexto();
        var admin = new AdminService(contexto);
        if (desativarPeloProprioUsuario)
            Assert.True(new UsuarioService(contexto).DesativarConta(banco.Usuario.Id));
        else
            await admin.AlterarSituacaoUsuario(banco.Administrador.Id, banco.Usuario.Id, false);
        var versaoDesativada = contexto.Usuarios.AsNoTracking().Single(u => u.Id == banco.Usuario.Id).VersaoSessao;
        Assert.NotEqual(banco.Usuario.VersaoSessao, versaoDesativada);
        Assert.False((await banco.AutenticarCookie(cookie)).Succeeded);
        await admin.AlterarSituacaoUsuario(banco.Administrador.Id, banco.Usuario.Id, true);
        Assert.NotEqual(versaoDesativada, contexto.Usuarios.AsNoTracking().Single(u => u.Id == banco.Usuario.Id).VersaoSessao);
        Assert.False((await banco.AutenticarCookie(cookie)).Succeeded);
        var usuario = new UsuarioService(contexto).Autenticar(new() { Email = banco.Usuario.Email, Senha = SenhaOriginal });
        Assert.NotNull(usuario);
        var cookieNovo = await banco.CriarCookie(usuario.VersaoSessao.ToString());
        Assert.True((await banco.AutenticarCookie(cookieNovo)).Succeeded);
    }

    [Fact]
    public async Task ControllerRenovaSomenteOCookieQueAlterouASenha()
    {
        using var banco = new BancoSessoes();
        var cookieAntigo = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        using var escopo = banco.Servicos.CreateScope();
        var http = banco.CriarHttp(escopo.ServiceProvider, cookieAntigo);
        http.User = (await http.AuthenticateAsync()).Principal!;
        var controller = new ContaController(new UsuarioService(escopo.ServiceProvider.GetRequiredService<AppDbContext>()), new EmailServiceFalso())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var resultado = await controller.AlterarSenha(new AlterarSenhaViewModel
        {
            SenhaAtual = SenhaOriginal, NovaSenha = NovaSenha, ConfirmarNovaSenha = NovaSenha
        });
        Assert.IsType<OkObjectResult>(resultado);
        var cookieNovo = BancoSessoes.LerCookie(http);
        Assert.True((await banco.AutenticarCookie(cookieNovo)).Succeeded);
        Assert.False((await banco.AutenticarCookie(cookieAntigo)).Succeeded);
    }

    [Fact]
    public async Task AtualizacaoDePerfilConcorrenteNaoRessuscitaSessaoRevogada()
    {
        using var banco = new BancoSessoes();
        var cookie = await banco.CriarCookie(banco.Usuario.VersaoSessao.ToString());
        using var escopo = banco.Servicos.CreateScope();
        var http = banco.CriarHttp(escopo.ServiceProvider, cookie);
        http.User = (await http.AuthenticateAsync()).Principal!;
        // A requisição começou autenticada, mas outra sessão muda a senha antes da renovação do perfil.
        using (var outroContexto = banco.CriarContexto())
            Assert.NotNull(new UsuarioService(outroContexto).AlterarSenha(banco.Usuario.Id, Alteracao(), banco.Usuario.VersaoSessao));
        var controller = new ContaController(new UsuarioService(escopo.ServiceProvider.GetRequiredService<AppDbContext>()), new EmailServiceFalso())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var resultado = await controller.EditarPerfil(new EditarPerfilViewModel { Nome = "Nome editado", Email = banco.Usuario.Email });
        Assert.IsType<OkObjectResult>(resultado);
        Assert.False((await banco.AutenticarCookie(BancoSessoes.LerCookie(http))).Succeeded);
    }

    private static UsuarioAlterarSenhaDTO Alteracao() => new()
    {
        SenhaAtual = SenhaOriginal, NovaSenha = NovaSenha, ConfirmarNovaSenha = NovaSenha
    };

    private sealed class BancoSessoes : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly DbContextOptions<AppDbContext> _opcoes;
        public ServiceProvider Servicos { get; }
        public Usuario Usuario { get; }
        public Usuario Administrador { get; }

        public BancoSessoes()
        {
            _conexao.Open();
            _opcoes = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options;
            using var contexto = CriarContexto();
            contexto.Database.EnsureCreated();
            Usuario = new Usuario { Nome = "Cliente", Email = "cliente@teste.local" };
            Usuario.SenhaHash = new PasswordHasher<Usuario>().HashPassword(Usuario, SenhaOriginal);
            Administrador = new Usuario { Nome = "Admin", Email = "admin@teste.local", Tipo = TipoUsuario.Administrador, SenhaHash = "nao-utilizado" };
            contexto.AddRange(Usuario, Administrador);
            contexto.SaveChanges();
            var servicos = new ServiceCollection();
            servicos.AddLogging();
            servicos.AddDataProtection().UseEphemeralDataProtectionProvider();
            servicos.AddScoped(_ => CriarContexto());
            servicos.AddScoped<ValidarSessaoUsuario>();
            servicos.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opcoes =>
                opcoes.EventsType = typeof(ValidarSessaoUsuario));
            Servicos = servicos.BuildServiceProvider();
        }

        public AppDbContext CriarContexto() => new(_opcoes);

        public DefaultHttpContext CriarHttp(IServiceProvider servicos, string? cookie = null)
        {
            var http = new DefaultHttpContext { RequestServices = servicos };
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("localhost");
            http.Request.Path = "/Conta/MeuPerfil";
            if (cookie != null) http.Request.Headers.Cookie = cookie;
            return http;
        }

        public async Task<string> CriarCookie(string? versao)
        {
            using var escopo = Servicos.CreateScope();
            var http = CriarHttp(escopo.ServiceProvider);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Usuario.Id.ToString()),
                new(ClaimTypes.Name, Usuario.Nome),
                new(ClaimTypes.Email, Usuario.Email),
                new(ClaimTypes.Role, Usuario.Tipo.ToString())
            };
            if (versao != null) claims.Add(new(ValidarSessaoUsuario.ClaimVersaoSessao, versao));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return LerCookie(http);
        }

        public async Task<AuthenticateResult> AutenticarCookie(string cookie)
        {
            using var escopo = Servicos.CreateScope();
            return await CriarHttp(escopo.ServiceProvider, cookie).AuthenticateAsync();
        }

        public static string LerCookie(HttpContext http) => http.Response.Headers.SetCookie.ToString().Split(';')[0];

        public void Dispose()
        {
            Servicos.Dispose();
            _conexao.Dispose();
        }
    }
}
