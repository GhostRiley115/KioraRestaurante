using System.Net;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

public class CombosHistoricoTests
{
    [Fact]
    public async Task DetalhesDoClienteEAdministradorPreservamComposicaoAposAlteracaoDoCatalogo()
    {
        using var banco = new BancoCombos();
        var composicaoComprada = ProdutoVenda.DescreverComposicao(banco.Combo);
        var pedido = banco.CriarPedido(composicaoComprada);

        banco.Combo.Nome = "Combo alterado";
        banco.Componente.Nome = "Produto alterado";
        banco.Combo.Componentes.Single().Quantidade = 3;
        banco.Contexto.SaveChanges();
        Assert.NotEqual(composicaoComprada, ProdutoVenda.DescreverComposicao(banco.Combo));
        banco.Contexto.ChangeTracker.Clear();

        var detalhesCliente = await banco.Pedidos.BuscarPedido(banco.Cliente.Id, pedido.Id);
        var detalhesAdmin = await new AdminService(banco.Contexto).DetalhesPedido(banco.Admin.Id, pedido.Id);
        Assert.NotNull(detalhesCliente);
        Assert.NotNull(detalhesAdmin);
        Assert.Equal(composicaoComprada, Assert.Single(detalhesCliente.ItensPedido).Composicao);
        Assert.Equal(composicaoComprada, Assert.Single(detalhesAdmin.Itens).Composicao);

        await using var telas = new TelasRazor();
        foreach (var html in new[]
                 {
                     await telas.Renderizar("/Views/Pedido/Detalhes.cshtml", detalhesCliente),
                     await telas.Renderizar("/Views/Admin/Pedidos/Detalhes.cshtml", detalhesAdmin)
                 })
        {
            Assert.Contains("Composição por combo:", WebUtility.HtmlDecode(html));
            Assert.Contains(composicaoComprada!, WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("Produto alterado", html);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PedidoLegadoSemComposicaoNaoInventaDadosDoCatalogo(string? composicao)
    {
        using var banco = new BancoCombos();
        var pedido = banco.CriarPedido(composicao);
        var detalhesCliente = await banco.Pedidos.BuscarPedido(banco.Cliente.Id, pedido.Id);
        var detalhesAdmin = await new AdminService(banco.Contexto).DetalhesPedido(banco.Admin.Id, pedido.Id);
        Assert.Equal(composicao, Assert.Single(detalhesCliente!.ItensPedido).Composicao);
        Assert.Equal(composicao, Assert.Single(detalhesAdmin!.Itens).Composicao);

        await using var telas = new TelasRazor();
        Assert.DoesNotContain("Composição por combo:", WebUtility.HtmlDecode(
            await telas.Renderizar("/Views/Pedido/Detalhes.cshtml", detalhesCliente)));
        Assert.DoesNotContain("Composição por combo:", WebUtility.HtmlDecode(
            await telas.Renderizar("/Views/Admin/Pedidos/Detalhes.cshtml", detalhesAdmin)));
    }

    [Fact]
    public async Task ComposicaoEExibidaComoTextoSemExecutarHtmlNosDetalhesENoCheckout()
    {
        const string composicao = "2 × <script>alert('teste')</script> & molho";
        using var banco = new BancoCombos();
        var pedido = banco.CriarPedido(composicao);
        var detalhesAdmin = await new AdminService(banco.Contexto).DetalhesPedido(banco.Admin.Id, pedido.Id);
        var checkout = new CheckoutViewModel
        {
            Resumo = new()
            {
                Itens = [new ItemCarrinhoResponseDTO { NomeProduto = "Combo", Composicao = composicao }]
            }
        };

        await using var telas = new TelasRazor();
        foreach (var html in new[]
                 {
                     await telas.Renderizar("/Views/Pedido/Detalhes.cshtml", pedido),
                     await telas.Renderizar("/Views/Admin/Pedidos/Detalhes.cshtml", detalhesAdmin!),
                     await telas.Renderizar("/Views/Pedido/Checkout.cshtml", checkout)
                 })
        {
            Assert.Contains(composicao, WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("<script>alert", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("&amp; molho", html);
        }
    }

    private sealed class BancoCombos : IDisposable
    {
        private readonly SqliteConnection _conexao = new("Data Source=:memory:");
        private readonly HttpClient _http = new();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public AppDbContext Contexto { get; }
        public Usuario Cliente { get; }
        public Usuario Admin { get; }
        public Produto Combo { get; }
        public Produto Componente { get; }
        public PedidoService Pedidos { get; }

        public BancoCombos()
        {
            _conexao.Open();
            Contexto = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexao).Options);
            Contexto.Database.EnsureCreated();
            Cliente = new Usuario { Nome = "Cliente", Email = "cliente@teste.local", SenhaHash = "nao-utilizada" };
            Admin = new Usuario { Nome = "Admin", Email = "admin@teste.local", SenhaHash = "nao-utilizada", Tipo = TipoUsuario.Administrador };
            Componente = new Produto { Nome = "Hambúrguer original", Descricao = "Produto de teste", CategoriaId = 2, Preco = 15m };
            Combo = new Produto
            {
                Nome = "Combo original", Descricao = "Combo de teste", CategoriaId = 2, Preco = 25m, EhCombo = true,
                Componentes = [new ComboComponente { Produto = Componente, Quantidade = 2 }]
            };
            Contexto.AddRange(Cliente, Admin, Combo);
            Contexto.SaveChanges();
            var cep = new CepService(_http, _cache, new ConfigurationBuilder().Build());
            Pedidos = new PedidoService(Contexto, new CarrinhoService(Contexto),
                new EphemeralDataProtectionProvider(), new EnderecoUsuarioService(Contexto, cep));
        }

        public Pedido CriarPedido(string? composicao)
        {
            var pedido = new Pedido
            {
                UsuarioId = Cliente.Id, ValorTotal = 25m,
                ItensPedido = [new ItemPedido
                {
                    ProdutoId = Combo.Id, NomeProduto = "Combo original", Quantidade = 1,
                    PrecoUnitario = 25m, Composicao = composicao
                }]
            };
            Contexto.Pedidos.Add(pedido);
            Contexto.SaveChanges();
            return pedido;
        }

        public void Dispose()
        {
            Contexto.Dispose();
            _conexao.Dispose();
            _http.Dispose();
            _cache.Dispose();
        }
    }

    // Renderiza as views reais sem iniciar o site nem conectar ao banco configurado da aplicação.
    private sealed class TelasRazor : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public TelasRazor()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(AdminService).Assembly.GetName().Name,
                EnvironmentName = "Testing",
                ContentRootPath = Path.GetTempPath()
            });
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AdminService).Assembly);
            _app = builder.Build();
            _app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        }

        public async Task<string> Renderizar<T>(string caminho, T model)
        {
            using var escopo = _app.Services.CreateScope();
            var http = new DefaultHttpContext { RequestServices = escopo.ServiceProvider };
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("localhost");
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Teste Razor"));
            var actionContext = new ActionContext(http, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
            var engine = escopo.ServiceProvider.GetRequiredService<IRazorViewEngine>();
            var resultado = engine.GetView(null, caminho, false);
            Assert.True(resultado.Success, $"View não encontrada: {caminho}");
            await using var writer = new StringWriter();
            var viewData = new ViewDataDictionary<T>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            var tempData = new TempDataDictionary(http, escopo.ServiceProvider.GetRequiredService<ITempDataProvider>());
            var viewContext = new ViewContext(actionContext, resultado.View, viewData, tempData, writer, new HtmlHelperOptions());
            await resultado.View.RenderAsync(viewContext);
            return writer.ToString();
        }

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }
}
