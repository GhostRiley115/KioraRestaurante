using System.Text.Json;
using KioraRestaurante.Controllers;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

public class NavegacaoTests
{
    [Theory]
    [InlineData("/Pedido/Checkout?origem=menu", "/Pedido/Checkout?origem=menu")]
    [InlineData("~/Pedido/Checkout", "/Pedido/Checkout")]
    [InlineData("/Conta/MeuPerfil", "/Conta/MeuPerfil")]
    [InlineData("https://externo.example/", null)]
    [InlineData("//externo.example/", null)]
    [InlineData("/\\externo.example/", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("/\n/externo.example/", null)]
    [InlineData(null, null)]
    public async Task LoginPreservaSomenteRetornosLocaisNoGetENoPost(string? solicitado, string? esperado)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddDataProtection().UseEphemeralDataProtectionProvider();
        servicos.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        using var provider = servicos.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("localhost");
        var controller = new ContaController(new UsuarioLoginFalso(), new EmailServiceFalso())
        {
            ControllerContext = new ControllerContext { HttpContext = http, RouteData = new RouteData() }
        };
        controller.Url = new UrlHelper(controller.ControllerContext);
        var get = Assert.IsType<RedirectToActionResult>(controller.Login(solicitado));
        Assert.Equal("Home", get.ControllerName);
        Assert.Equal(esperado, get.RouteValues!["returnUrl"]);
        var post = Assert.IsType<OkObjectResult>(await controller.Login(new LoginViewModel
        {
            Email = "cliente@teste.local", Senha = "Teste123!", ReturnUrl = solicitado
        }));
        var json = JsonSerializer.SerializeToElement(post.Value);
        Assert.True(json.GetProperty("sucesso").GetBoolean());
        Assert.Equal(esperado, json.GetProperty("redirecionarPara").GetString());
    }

    private sealed class UsuarioLoginFalso : IUsuarioService
    {
        public UsuarioResponseDTO? Autenticar(UsuarioLoginDTO dto) => new()
        {
            Id = 1, Nome = "Cliente", Email = dto.Email, Tipo = TipoUsuario.Cliente,
            Ativo = true, VersaoSessao = Guid.NewGuid()
        };
        public bool EmailExiste(string email) => throw new NotSupportedException();
        public UsuarioResponseDTO Cadastrar(UsuarioCadastroDTO dto) => throw new NotSupportedException();
        public RecuperacaoSenhaGeradaDTO? GerarTokenRecuperacao(UsuarioSolicitarRecuperacaoDTO dto) => throw new NotSupportedException();
        public bool ValidarCodigoRecuperacao(string email, string codigo) => throw new NotSupportedException();
        public void InvalidarCodigoRecuperacao(RecuperacaoSenhaGeradaDTO recuperacao) => throw new NotSupportedException();
        public bool RedefinirSenha(UsuarioRedefinirSenhaDTO dto) => throw new NotSupportedException();
        public UsuarioResponseDTO? BuscarPorId(int usuarioId) => throw new NotSupportedException();
        public bool EmailExisteParaOutroUsuario(string email, int usuarioId) => throw new NotSupportedException();
        public bool AtualizarPerfil(int usuarioId, UsuarioAtualizarPerfilDTO dto) => throw new NotSupportedException();
        public Guid? AlterarSenha(int usuarioId, UsuarioAlterarSenhaDTO dto, Guid versaoSessao) => throw new NotSupportedException();
        public bool DesativarConta(int usuarioId) => throw new NotSupportedException();
    }
}
