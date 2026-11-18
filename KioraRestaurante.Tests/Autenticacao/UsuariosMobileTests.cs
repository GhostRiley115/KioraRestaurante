using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using KioraRestaurante.Controllers.Api;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Filters;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Reservas;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using KioraRestaurante.Services.Autenticacao;
using KioraRestaurante.Services.Usuarios;
using KioraRestaurante.ViewModels.Conta;

namespace KioraRestaurante.Tests;

public class UsuariosMobileTests
{
    [Fact]
    public async Task LoginJsonEFormularioCompartilhamTentativasDaMesmaContaIncluindoCollation()
    {
        using var banco = BancoLogin();
        banco.Usuarios.Add(new Usuario { Nome = "Cliente", Email = "clíente@example.com", SenhaHash = "não-usado" });
        banco.SaveChanges();
        using var limites = new LimitadorLogin();
        var filtro = new LimitarTentativasLogin(limites, banco);
        var executados = 0;
        for (var i = 0; i < 6; i++)
        {
            object dto = i % 2 == 0
                ? new LoginViewModel { Email = "  CLIENTE@EXAMPLE.COM  ", Senha = "qualquer" }
                : new UsuarioLoginDTO { Email = "clíente@example.com", Senha = "qualquer" };
            var contexto = Acao(dto);
            await filtro.OnActionExecutionAsync(contexto, () =>
            {
                executados++;
                return Task.FromResult(new ActionExecutedContext(contexto, contexto.Filters, contexto.Controller));
            });
            if (i == 5)
            {
                Assert.Equal(429, Assert.IsType<JsonResult>(contexto.Result).StatusCode);
                Assert.InRange(int.Parse(contexto.HttpContext.Response.Headers.RetryAfter.ToString()), 1, 60);
            }
        }
        Assert.Equal(5, executados);
    }

    [Fact]
    public async Task LoginJsonComEmailGrandeNaoChegaAConsultaDaSenha()
    {
        using var banco = BancoLogin();
        using var limites = new LimitadorLogin();
        var contexto = Acao(new UsuarioLoginDTO { Email = new string('a', 151) + "@example.com", Senha = "qualquer" });
        await new LimitarTentativasLogin(limites, banco).OnActionExecutionAsync(contexto,
            () => throw new InvalidOperationException("Não pode executar o login."));
        Assert.IsType<BadRequestObjectResult>(contexto.Result);
    }

    [Fact]
    public void TrocaDeSenhaNaoEmiteTokenAdotandoRevogacaoConcorrente()
    {
        var servico = new UsuarioFalso { VersaoDaAlteracao = Guid.NewGuid() };
        servico.Usuario.VersaoSessao = Guid.NewGuid();
        var jwt = new JwtFalso();
        var resultado = Controller(servico, jwt).AlterarSenha(Alteracao());
        Assert.IsType<UnauthorizedObjectResult>(resultado);
        Assert.Null(jwt.Usuario);
    }

    [Fact]
    public void TrocaDeSenhaEmiteSomenteAVersaoDaPropriaAlteracao()
    {
        var servico = new UsuarioFalso { VersaoDaAlteracao = Guid.NewGuid() };
        servico.Usuario.VersaoSessao = servico.VersaoDaAlteracao.Value;
        var jwt = new JwtFalso();
        var resultado = Assert.IsType<OkObjectResult>(Controller(servico, jwt).AlterarSenha(Alteracao()));
        Assert.Equal(servico.VersaoDaAlteracao, jwt.Usuario!.VersaoSessao);
        Assert.Equal("token-de-teste", JsonSerializer.SerializeToElement(resultado.Value).GetProperty("token").GetString());
    }

    [Fact]
    public async Task CadastroSimultaneoQuePerdeUnicidadeRetorna409SemDetalheDoBanco()
    {
        var servico = new UsuarioFalso { FalhaAoSalvar = new DbUpdateException("DETALHE-INTERNO-DO-BANCO") };
        var resultado = Assert.IsType<ConflictObjectResult>(await Controller(servico, new()).Cadastrar(new()
        {
            Nome = "Cliente", Email = "cliente@example.com", Senha = "Senha original 123", ConfirmarSenha = "Senha original 123", AceiteTermos = true
        }));
        Assert.DoesNotContain("DETALHE-INTERNO", JsonSerializer.Serialize(resultado.Value));
    }

    [Fact]
    public void EdicaoSimultaneaQuePerdeUnicidadeRetorna409SemDetalheDoBanco()
    {
        var servico = new UsuarioFalso { FalhaAoSalvar = new DbUpdateException("DETALHE-INTERNO-DO-BANCO") };
        var resultado = Assert.IsType<ConflictObjectResult>(Controller(servico, new()).AtualizarPerfil(new()
        {
            Nome = "Cliente", Email = "cliente@example.com"
        }));
        Assert.DoesNotContain("DETALHE-INTERNO", JsonSerializer.Serialize(resultado.Value));
    }

    [Fact]
    public async Task ValidacaoDoServicoNoCadastroRetorna400()
    {
        var servico = new UsuarioFalso { FalhaAoSalvar = new ValidationException("Confira os dados.") };
        var resultado = Assert.IsType<BadRequestObjectResult>(await Controller(servico, new()).Cadastrar(new()));
        Assert.Equal("Confira os dados.", JsonSerializer.SerializeToElement(resultado.Value).GetProperty("mensagem").GetString());
    }

    [Fact]
    public void LogoutUsaVersaoDaRequisicaoERecusaSessaoJaRevogada()
    {
        var servico = new UsuarioFalso { RevogacaoPermitida = true };
        var controller = Controller(servico, new());
        Assert.IsType<OkObjectResult>(controller.Logout());
        Assert.Equal(controller.User.FindFirst("VersaoSessao")!.Value, servico.VersaoRecebida.ToString());
        servico.RevogacaoPermitida = false;
        Assert.IsType<UnauthorizedObjectResult>(controller.Logout());
    }

    [Fact]
    public void LogoutNaoExecutaSemVersaoDeSessao()
    {
        var servico = new UsuarioFalso { RevogacaoPermitida = true };
        var controller = Controller(servico, new());
        controller.HttpContext.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        Assert.IsType<UnauthorizedObjectResult>(controller.Logout());
        Assert.Null(servico.VersaoRecebida);
    }

    private static UsuarioAlterarSenhaDTO Alteracao() => new()
    {
        SenhaAtual = "Senha original 123", NovaSenha = "Nova senha 456", ConfirmarNovaSenha = "Nova senha 456"
    };

    private static UsuarioApiController Controller(UsuarioFalso servico, JwtFalso jwt)
    {
        var controller = new UsuarioApiController(servico, jwt, new ReservaService(null!, null!),
            new IdentidadeReserva(new EphemeralDataProtectionProvider(), null!));
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.User = new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, "1"), new("VersaoSessao", Guid.NewGuid().ToString())], "Bearer"));
        return controller;
    }

    private static ActionExecutingContext Acao(object dto)
    {
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        return new(action, [], new Dictionary<string, object?> { ["dto"] = dto }, new object());
    }

    private static AppDbContext BancoLogin()
    {
        var banco = new BancoComCollation(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        banco.Database.OpenConnection();
        ((SqliteConnection)banco.Database.GetDbConnection()).CreateCollation("NOCASE",
            (a, b) => CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        banco.Database.EnsureCreated();
        return banco;
    }

    private sealed class BancoComCollation(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Usuario>().Property(u => u.Email).UseCollation("NOCASE");
        }
    }

    private sealed class JwtFalso : IJwtService
    {
        public UsuarioResponseDTO? Usuario { get; private set; }
        public string GerarToken(UsuarioResponseDTO usuario) { Usuario = usuario; return "token-de-teste"; }
    }

    private sealed class UsuarioFalso : IUsuarioService
    {
        public UsuarioResponseDTO Usuario { get; } = new() { Id = 1, Nome = "Cliente", Email = "cliente@example.com", Ativo = true };
        public Guid? VersaoDaAlteracao { get; init; }
        public Exception? FalhaAoSalvar { get; init; }
        public bool RevogacaoPermitida { get; set; }
        public Guid? VersaoRecebida { get; private set; }
        public bool EmailExiste(string email) => false;
        public UsuarioResponseDTO Cadastrar(UsuarioCadastroDTO dto) { if (FalhaAoSalvar != null) throw FalhaAoSalvar; return Usuario; }
        public UsuarioResponseDTO? Autenticar(UsuarioLoginDTO dto) => Usuario;
        public UsuarioResponseDTO? BuscarPorId(int usuarioId) => Usuario;
        public bool EmailExisteParaOutroUsuario(string email, int usuarioId) => false;
        public bool AtualizarPerfil(int usuarioId, UsuarioAtualizarPerfilDTO dto) { if (FalhaAoSalvar != null) throw FalhaAoSalvar; return true; }
        public Guid? AlterarSenha(int usuarioId, UsuarioAlterarSenhaDTO dto, Guid versaoSessao) => VersaoDaAlteracao;
        public bool RevogarSessoes(int usuarioId, Guid versaoSessao) { VersaoRecebida = versaoSessao; return RevogacaoPermitida; }
        public bool DesativarConta(int usuarioId) => throw new NotSupportedException();
        public bool DesativarConta(int usuarioId, Guid versaoSessao) => throw new NotSupportedException();
        public RecuperacaoSenhaGeradaDTO? GerarTokenRecuperacao(UsuarioSolicitarRecuperacaoDTO dto) => throw new NotSupportedException();
        public bool ValidarCodigoRecuperacao(string email, string codigo) => throw new NotSupportedException();
        public void InvalidarCodigoRecuperacao(RecuperacaoSenhaGeradaDTO recuperacao) => throw new NotSupportedException();
        public bool RedefinirSenha(UsuarioRedefinirSenhaDTO dto) => throw new NotSupportedException();
    }
}
