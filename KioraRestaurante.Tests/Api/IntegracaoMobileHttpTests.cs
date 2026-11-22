using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KioraRestaurante.Controllers.Api;
using KioraRestaurante.Data;
using KioraRestaurante.Filters;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Cardapio;
using KioraRestaurante.Services.Notificacoes;
using KioraRestaurante.Services.Reservas;
using KioraRestaurante.Services.Salao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using KioraRestaurante.Configuracao;
using KioraRestaurante.Services.Autenticacao;
using KioraRestaurante.Services.Email;
using KioraRestaurante.Services.Imagens;
using KioraRestaurante.Services.Produtos;
using KioraRestaurante.Services.Usuarios;
using KioraRestaurante.Services.Admin;
using KioraRestaurante.Services.Pedidos;
using KioraRestaurante.Services.Enderecos;
using KioraRestaurante.Services.Entrega;
using KioraRestaurante.Services.PagamentosOnline;
using KioraRestaurante.Services.Cupons;

namespace KioraRestaurante.Tests;

// Usa o pipeline de autenticação/CSRF real, banco temporário e nenhuma integração externa.
public class IntegracaoMobileHttpTests
{
    [Fact] public async Task TokenSoAcessaApisECargosSaoConferidosNoServidor()
    {
        await using var b=await HostMobile.Criar();using var http=b.Cliente();
        Assert.Equal(HttpStatusCode.OK,(await http.GetAsync("/api/cardapio/produtos")).StatusCode);
        var semSessao=await http.GetAsync("/api/usuarios/perfil");Assert.Equal(HttpStatusCode.Unauthorized,semSessao.StatusCode);Assert.Contains("json",semSessao.Content.Headers.ContentType!.MediaType);
        foreach(var cargo in Enum.GetValues<TipoUsuario>())
        {
            http.DefaultRequestHeaders.Authorization=new("Bearer",b.Token(cargo));
            var esperado=cargo is TipoUsuario.Cliente or TipoUsuario.Cozinha?HttpStatusCode.Forbidden:HttpStatusCode.OK;
            Assert.Equal(esperado,(await http.GetAsync("/api/salao/mesas")).StatusCode);
        }
        http.DefaultRequestHeaders.Authorization=new("Bearer",b.Token(TipoUsuario.Cliente));
        Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync("/api/admin/produtos")).StatusCode);
        var web=await http.GetAsync("/Pedido/MeusPedidos");Assert.Equal(HttpStatusCode.Redirect,web.StatusCode);Assert.Contains("/Conta/Login",web.Headers.Location!.OriginalString);
        http.DefaultRequestHeaders.Authorization=new("Bearer","token-invalido");
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync("/api/usuarios/perfil")).StatusCode);
    }

    [Fact] public async Task VisitantePrecisaCsrfMasBearerValidoGravaSemCookies()
    {
        await using var b=await HostMobile.Criar();using var visitante=b.Cliente();
        var sessao=await visitante.GetFromJsonAsync<JsonElement>("/api/reservas/sessao");
        visitante.DefaultRequestHeaders.Add("X-Reserva-Chave",sessao.GetProperty("chaveVisitante").GetString());
        var dto=b.Reserva();
        Assert.Equal(HttpStatusCode.BadRequest,(await visitante.PostAsJsonAsync("/api/reservas",dto)).StatusCode);
        visitante.DefaultRequestHeaders.Add("X-CSRF-TOKEN",sessao.GetProperty("csrfToken").GetString());
        var criada=await visitante.PostAsJsonAsync("/api/reservas",dto);Assert.Equal(HttpStatusCode.OK,criada.StatusCode);
        var reserva=await criada.Content.ReadFromJsonAsync<JsonElement>();
        var login=await visitante.PostAsJsonAsync("/api/usuarios/login",new{email="cliente@mobile.test",senha=HostMobile.Senha});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);var token=(await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var app=b.Cliente(cookies:false);app.DefaultRequestHeaders.Authorization=new("Bearer",token);
        var historico=await app.GetFromJsonAsync<JsonElement>("/api/reservas/minhas");Assert.Single(historico.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,(await app.PostAsJsonAsync("/api/reservas",b.Reserva("15:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await app.PostAsJsonAsync("/api/reservas/cancelar",new{codigo=reserva.GetProperty("codigo").GetGuid(),versao=reserva.GetProperty("versao").GetGuid()})).StatusCode);
        app.DefaultRequestHeaders.Authorization=new("Bearer",b.Token(TipoUsuario.Cliente,outroCliente:true));
        Assert.Equal(HttpStatusCode.NotFound,(await app.GetAsync("/api/reservas/"+reserva.GetProperty("codigo").GetGuid())).StatusCode);
    }

    [Fact] public async Task CookieDaEquipeMantemCsrfEHeaderBearerFalsoNaoDispensaProtecao()
    {
        await using var b=await HostMobile.Criar();using var web=b.Cliente();
        var anon=await web.GetFromJsonAsync<JsonElement>("/api/equipe/sessao");web.DefaultRequestHeaders.Add("X-CSRF-TOKEN",anon.GetProperty("csrfToken").GetString());
        var login=await web.PostAsync("/api/equipe/login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email","garcom@mobile.test"},{"Senha",HostMobile.Senha}}));Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var mesa=(await web.GetFromJsonAsync<JsonElement>("/api/salao/mesas")).EnumerateArray().First();
        var comando=new{mesaId=mesa.GetProperty("id").GetInt32(),versao=mesa.GetProperty("versao").GetGuid(),pessoas=2,chaveSolicitacao=Guid.NewGuid()};
        Assert.Equal(HttpStatusCode.BadRequest,(await web.PostAsJsonAsync("/api/salao/acomodar",comando)).StatusCode);
        web.DefaultRequestHeaders.Authorization=new("Bearer","falso");
        Assert.Equal(HttpStatusCode.Unauthorized,(await web.PostAsJsonAsync("/api/salao/acomodar",comando)).StatusCode);
        web.DefaultRequestHeaders.Authorization=null;
        var sessao=await web.GetFromJsonAsync<JsonElement>("/api/equipe/sessao");web.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");web.DefaultRequestHeaders.Add("X-CSRF-TOKEN",sessao.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.OK,(await web.PostAsJsonAsync("/api/salao/acomodar",comando)).StatusCode);
        await using var scope=b.App.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.AtendimentosMesa.ToListAsync());Assert.Empty(await db.Pedidos.ToListAsync());Assert.Empty(await db.PagamentosOnline.ToListAsync());
    }
[Fact] public async Task LoginJsonNaoContornaCotaConsumidaPeloFormulario()
{
    await using var b = await HostMobile.Criar(); using var web = b.Cliente();
    var sessao = await web.GetFromJsonAsync<JsonElement>("/api/equipe/sessao"); web.DefaultRequestHeaders.Add("X-CSRF-TOKEN", sessao.GetProperty("csrfToken").GetString());
    for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.BadRequest, (await web.PostAsync("/api/equipe/login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", "GARCOM@MOBILE.TEST" }, { "Senha", "incorreta" } }))).StatusCode);
    for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await web.PostAsJsonAsync("/api/usuarios/login", new { email = "garcom@mobile.test", senha = "incorreta" })).StatusCode);
    var bloqueado = await web.PostAsJsonAsync("/api/usuarios/login", new { email = "garcom@mobile.test", senha = HostMobile.Senha });
    Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode); Assert.NotNull(bloqueado.Headers.RetryAfter);
}

[Fact] public async Task LogoutMobileRevogaTokensAnterioresELogoutCookieMantemContratoWeb()
{
    await using var b = await HostMobile.Criar(); using var app = b.Cliente(false); var primeiro = b.Token(TipoUsuario.Cliente); var segundo = b.Token(TipoUsuario.Cliente);
    app.DefaultRequestHeaders.Authorization = new("Bearer", primeiro);
    Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync("/api/usuarios/logout", new { })).StatusCode);
    foreach (var token in new[] { primeiro, segundo })
    { app.DefaultRequestHeaders.Authorization = new("Bearer", token); Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/usuarios/perfil")).StatusCode); }
    using var web = b.Cliente(); var anon = await web.GetFromJsonAsync<JsonElement>("/api/equipe/sessao"); web.DefaultRequestHeaders.Add("X-CSRF-TOKEN", anon.GetProperty("csrfToken").GetString());
    Assert.Equal(HttpStatusCode.OK, (await web.PostAsync("/api/equipe/login", new FormUrlEncodedContent(new Dictionary<string, string> { { "Email", "garcom@mobile.test" }, { "Senha", HostMobile.Senha } }))).StatusCode);
    var sessao = await web.GetFromJsonAsync<JsonElement>("/api/equipe/sessao"); web.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); web.DefaultRequestHeaders.Add("X-CSRF-TOKEN", sessao.GetProperty("csrfToken").GetString());
    var jwt = b.Token(TipoUsuario.Garcom); Assert.Equal(HttpStatusCode.OK, (await web.PostAsJsonAsync("/api/equipe/logout", new { })).StatusCode);
    app.DefaultRequestHeaders.Authorization = new("Bearer", jwt); Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/api/usuarios/perfil")).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync("/api/equipe/logout", new { })).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await app.GetAsync("/api/usuarios/perfil")).StatusCode);
}

[Fact] public async Task PatchVazioOuAntigoNaoMudaMesaENaoDuplicaMovimentos()
{
    await using var b = await HostMobile.Criar(); using var app = b.Cliente(false); app.DefaultRequestHeaders.Authorization = new("Bearer", b.Token(TipoUsuario.Administrador));
    var mesa = (await app.GetFromJsonAsync<JsonElement>("/api/salao/mesas")).EnumerateArray().First(); var id = mesa.GetProperty("id").GetInt32(); var revisao = mesa.GetProperty("versao").GetGuid(); var url = $"/api/reservas/admin/mesas/{id}/ativa";
    Assert.Equal(HttpStatusCode.BadRequest, (await app.PatchAsJsonAsync(url, new { })).StatusCode);
    var comando = new { ativa = false, versao = revisao, chaveSolicitacao = Guid.NewGuid() };
    Assert.Equal(HttpStatusCode.OK, (await app.PatchAsJsonAsync(url, comando)).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await app.PatchAsJsonAsync(url, comando)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await app.PatchAsJsonAsync(url, new { ativa = true, versao = revisao, chaveSolicitacao = Guid.NewGuid() })).StatusCode);
    await using var scope = b.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.False((await db.MesasRestaurante.SingleAsync(m => m.Id == id)).Ativa); Assert.Single(await db.MovimentosSalao.ToListAsync());
}

[Fact] public async Task ProdutosInvalidosRetornamJsonESemAlteracaoSilenciosa()
{
    await using var b = await HostMobile.Criar(); using var app = b.Cliente(false); app.DefaultRequestHeaders.Authorization = new("Bearer", b.Token(TipoUsuario.Administrador));
    foreach (var acao in new[] { "ativo", "disponibilidade", "desconto" })
    { var r = await app.PatchAsJsonAsync("/api/admin/produtos/1/" + acao, new { }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Contains("json", r.Content.Headers.ContentType!.MediaType); }
    using var form = new MultipartFormDataContent { { new StringContent("Prova"), "Nome" }, { new StringContent("Teste local"), "Descricao" }, { new StringContent("20"), "Preco" }, { new StringContent("999"), "CategoriaId" }, { new ByteArrayContent([0]), "foto", "teste.png" } };
    var resposta = await app.PostAsync("/api/admin/produtos", form); Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode); Assert.Contains("categoria", await resposta.Content.ReadAsStringAsync());
    await using var scope = b.App.Services.CreateAsyncScope(); var produto = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Produtos.SingleAsync(); Assert.True(produto.Ativo); Assert.True(produto.Disponivel); Assert.Equal(10, produto.DescontoPercentual);
}

[Fact] public async Task CotaDeReservasNaoPodeSerContornadaTrocandoJsonPorFormulario()
{
    await using var b = await HostMobile.Criar(); using var visitante = b.Cliente(); var sessao = await visitante.GetFromJsonAsync<JsonElement>("/api/reservas/sessao"); visitante.DefaultRequestHeaders.Add("X-CSRF-TOKEN", sessao.GetProperty("csrfToken").GetString());
    visitante.DefaultRequestHeaders.Add("X-Reserva-Chave", sessao.GetProperty("chaveVisitante").GetString());
    for (var i = 0; i < 12; i++) Assert.Equal(HttpStatusCode.BadRequest, (await visitante.PostAsJsonAsync("/api/reservas", new { })).StatusCode);
    var web = await visitante.PostAsync("/Reservas/Criar", new FormUrlEncodedContent([])); Assert.Equal(HttpStatusCode.TooManyRequests, web.StatusCode); Assert.NotNull(web.Headers.RetryAfter);
}

[Fact] public async Task CadastroPublicoVinculaReservaSemPermitirEscolhaDeCargo()
{
    await using var b = await HostMobile.Criar(); using var visitante = b.Cliente(); var sessao = await visitante.GetFromJsonAsync<JsonElement>("/api/reservas/sessao"); visitante.DefaultRequestHeaders.Add("X-Reserva-Chave", sessao.GetProperty("chaveVisitante").GetString()); visitante.DefaultRequestHeaders.Add("X-CSRF-TOKEN", sessao.GetProperty("csrfToken").GetString());
    Assert.Equal(HttpStatusCode.OK, (await visitante.PostAsJsonAsync("/api/reservas", b.Reserva())).StatusCode);
    var r = await visitante.PostAsJsonAsync("/api/usuarios/cadastro", new { nome = "Novo cliente", email = "novo@mobile.test", senha = HostMobile.Senha, confirmarSenha = HostMobile.Senha, aceiteTermos = true, tipo = 1 }); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    Assert.Equal(0, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("usuario").GetProperty("tipo").GetInt32());
    var login = await visitante.PostAsJsonAsync("/api/usuarios/login", new { email = "novo@mobile.test", senha = HostMobile.Senha }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    visitante.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
    Assert.Single((await visitante.GetFromJsonAsync<JsonElement>("/api/reservas/minhas")).EnumerateArray());
}

[Fact] public async Task ErrosInesperadosNaoExpoemExcecaoESwaggerDistingueConsultasPublicas()
{
    await using var b = await HostMobile.Criar(); using var http = b.Cliente(); var r = await http.GetAsync("/api/prova/erro"); Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode); var json = await r.Content.ReadAsStringAsync(); Assert.DoesNotContain("nao-expor-excecao", json); Assert.Contains("codigoSuporte", json);
    var doc = await http.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json"); Assert.Equal(0, doc.GetProperty("paths").GetProperty("/api/cardapio/produtos").GetProperty("get").GetProperty("security").GetArrayLength()); Assert.NotEmpty(doc.GetProperty("security").EnumerateArray());
}

[Theory][InlineData(null)][InlineData("")][InlineData("curta")]
public void ConfiguracaoSemChaveForteERecusadaAntesDoPrimeiroLogin(string? chave)
{
    var servicos = new ServiceCollection(); servicos.AddLogging(); servicos.AddAutenticacaoKiora(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:ChaveSecreta"] = chave }).Build());
    using var provider = servicos.BuildServiceProvider(); Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
}
}

internal sealed class HostMobile : IAsyncDisposable
{
    public const string Senha = "SomenteTeste123!";
    private readonly SqliteConnection _keeper;
    public WebApplication App { get; }
    private readonly Dictionary<TipoUsuario, int> _ids = [];
    private int _outroCliente;
    private Uri _origem = null!;
    private HostMobile(WebApplication app, SqliteConnection keeper) { App = app; _keeper = keeper; }
    public HttpClient Cliente(bool cookies = true) => new(new HttpClientHandler { UseCookies = cookies, CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = _origem };
    public object Reserva(string horario = "14:00") => new { nome = "Grupo fictício", telefone = "11900000000", pessoas = 2, data = "2026-11-17", horario, chaveSolicitacao = Guid.NewGuid() };
    public string Token(TipoUsuario cargo, bool outroCliente = false)
    { using var scope = App.Services.CreateScope(); return scope.ServiceProvider.GetRequiredService<IJwtService>().GerarToken(scope.ServiceProvider.GetRequiredService<IUsuarioService>().BuscarPorId(outroCliente ? _outroCliente : _ids[cargo])!); }
    public static async Task<HostMobile> Criar(IEmailService? emailService = null, HttpMessageHandler? provedorPagamento = null)
    {
        var conexao = $"Data Source=mobile-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10"; var keeper = new SqliteConnection(conexao); await keeper.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" }); builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:ChaveSecreta"] = "CHAVE-EXCLUSIVA-DE-TESTE-SEM-VALOR-REAL-MOBILE-20261117" }); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(conexao)); builder.Services.AddSingleton<TimeProvider>(new RelogioMobile()); builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAutenticacaoKiora(builder.Configuration);
        builder.Services.AddLimitesKiora();
        builder.Services.AddDocumentacaoApiKiora();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(UsuarioApiController).Assembly);
        // Aplica o padrão JSON do Kiora após registrar os serviços MVC. Mantém a mesma ordem do Program.cs.
        builder.Services.AddRespostasApiKiora();
        builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN"); builder.Services.AddScoped<ValidarSessaoUsuario>(); builder.Services.AddScoped<IUsuarioService, UsuarioService>(); builder.Services.AddScoped<IJwtService, JwtService>(); builder.Services.AddSingleton(new LimitadorLogin()); builder.Services.AddScoped<LimitarTentativasLogin>();
        builder.Services.AddOptions<ReservaOptions>(); builder.Services.AddScoped<HorariosReserva>(); builder.Services.AddScoped<ReservaService>(); builder.Services.AddScoped<IdentidadeReserva>(); builder.Services.AddScoped<PortariaService>(); builder.Services.AddScoped<AcessoSalaoService>(); builder.Services.AddScoped<ComandoSalaoService>(); builder.Services.AddScoped<ComandaService>(); 
        builder.Services.AddScoped<ConsultaSalaoService>(); builder.Services.AddScoped<AvisosSalaoService>(); builder.Services.AddScoped<CozinhaService>(); builder.Services.AddOptions<FirebaseOptions>(); builder.Services.AddScoped<FilaPushService>(); builder.Services.AddScoped<NotificacaoService>(); builder.Services.AddScoped<MaisPedidosService>();
        builder.Services.AddScoped<IProdutoService, ProdutoService>(); builder.Services.AddScoped<ICategoriaService, CategoriaService>(); builder.Services.AddScoped<CategoriaAdminService>(); builder.Services.AddScoped<CupomService>(); builder.Services.AddSingleton<IImagemProdutoService, ImagensProibidas>();
        // Reutiliza os limites reais de recuperacao de senha nos testes HTTP.
        builder.Services.AddSingleton(_ => new LimitadorRecuperacaoSenha());
        builder.Services.AddScoped<LimitarRecuperacaoSenha>();
        builder.Services.AddSingleton<IEmailService>(emailService ?? new EmailService(new HttpClient(new SemRede()), builder.Configuration, NullLogger<EmailService>.Instance));
        // Serviços administrativos utilizados pelo dashboard.
        builder.Services.AddScoped<IAdminService, AdminService>();
        builder.Services.AddScoped<RelatoriosAdminService>();
        // Serviços administrativos dos pedidos.
        builder.Services.AddScoped<AndamentoPedidoService>();
        builder.Services.AddScoped<GerenciarPedidoService>();
        // Serviços auxiliares.
        builder.Services.AddMemoryCache();
        builder.Services.AddOptions<EntregaOptions>();
        builder.Services.AddScoped<IRotasEntrega,RotasEntregaSimuladas>();
        builder.Services.AddScoped<CotacaoEntregaService>();
        builder.Services.AddScoped<EnderecoUsuarioService>();
        // Bloqueia consultas externas de CEP.
        builder.Services.AddHttpClient<CepService>(http =>
        {
            http.BaseAddress =
                new Uri("https://viacep.com.br/ws/");
        })
        .ConfigurePrimaryHttpMessageHandler(
            () => new SemRede());
        // Configuração financeira exclusivamente fictícia.
        builder.Services.Configure<MercadoPagoConfig>(
            config =>
            {
                config.Ativado = true;
                config.ModoTeste = true;
                config.AccessToken = "token-ficticio";
                config.ApplicationId = "456";
                config.VendedorId = "123";

                config.UrlPublica =
                    "https://kiora.example.com";
            });
        // Utiliza o simulador fornecido pelo teste.
        // Caso nenhum seja fornecido, bloqueia a rede.
        builder.Services.AddHttpClient<MpOrdersClient>(
            http =>
            {
                http.BaseAddress =
                    new Uri("https://api.mercadopago.com/");
            })
        .ConfigurePrimaryHttpMessageHandler(
            () => provedorPagamento ?? new SemRede());
        builder.Services.AddScoped<CheckoutOnlineService>();
        var app = builder.Build(); var host = new HostMobile(app, keeper);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            foreach (var cargo in Enum.GetValues<TipoUsuario>())
            { var nome = cargo == TipoUsuario.Cliente ? "cliente" : cargo == TipoUsuario.Garcom ? "garcom" : cargo.ToString().ToLowerInvariant(); var u = new Usuario { Nome = nome, Email = nome + "@mobile.test", Tipo = cargo }; u.SenhaHash = new PasswordHasher<Usuario>().HashPassword(u, Senha); db.Add(u); await db.SaveChangesAsync(); host._ids[cargo] = u.Id; }
            var outro = new Usuario { Nome = "Outro cliente", Email = "outro@mobile.test" }; outro.SenhaHash = new PasswordHasher<Usuario>().HashPassword(outro, Senha); db.Add(outro); db.Add(new Produto { Nome = "Lámen", Descricao = "Prova local", Preco = 20, CategoriaId = 2, DescontoPercentual = 10, Imagem = "/imagens/produto-sem-foto.svg" }); await db.SaveChangesAsync(); host._outroCliente = outro.Id;
        }
        app.UseSwagger(); app.UseRespostasApiKiora(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers(); app.MapDefaultControllerRoute();
        app.MapGet("/api/prova/erro", (HttpContext _) => Task.FromException(new InvalidOperationException("nao-expor-excecao")));
        await app.StartAsync(); host._origem = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()); return host;
    }
    public async ValueTask DisposeAsync() { await App.StopAsync(); await App.DisposeAsync(); await _keeper.DisposeAsync(); }
    private sealed class RelogioMobile : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero); }
    private sealed class SemRede : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => throw new InvalidOperationException("Integrações externas bloqueadas no teste."); }
    private sealed class ImagensProibidas : IImagemProdutoService { public Task<ImagemProdutoResultado> Enviar(IFormFile foto) => throw new InvalidOperationException("Upload externo bloqueado."); public Task<bool> Excluir(string publicId) => throw new InvalidOperationException("Exclusão externa bloqueada."); }
}