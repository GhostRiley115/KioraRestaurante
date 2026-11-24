using KioraRestaurante.Data;
using KioraRestaurante.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using KioraRestaurante.Configuracao;
using KioraRestaurante.Services.Admin;
using KioraRestaurante.Services.Autenticacao;
using KioraRestaurante.Services.Carrinhos;
using KioraRestaurante.Services.Email;
using KioraRestaurante.Services.Enderecos;
using KioraRestaurante.Services.Imagens;
using KioraRestaurante.Services.Pedidos;
using KioraRestaurante.Services.Produtos;
using KioraRestaurante.Services.Usuarios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocumentacaoApiKiora();

// Pega a string de conexão do arquivo appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// Descobre a versão do MySQL uma vez durante a inicialização.
var versaoMySql = ServerVersion.AutoDetect(connectionString);

// Configura o contexto do banco de dados.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, versaoMySql));

// Autenticação e comentários de sua configuração estão reunidos na extensão compartilhada.
builder.Services.AddAutenticacaoKiora(builder.Configuration);

// ======================================================
// CONTROLLERS E VIEWS
// ======================================================

builder.Services.AddControllersWithViews();
builder.Services.AddRespostasApiKiora();

// ======================================================
// INJEÇÃO DE DEPENDÊNCIAS
// ======================================================

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddHttpClient<IEmailService, EmailService>(
    http => http.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddDataProtection();
builder.Services.AddFirebaseKiora(builder.Configuration);
builder.Services.AddScoped<CarrinhoCookie>();
builder.Services.AddHostedService<LimpezaCarrinhosService>();

builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IImagemProdutoService, ImagemProdutoService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Serviços de cupons.
builder.Services.AddScoped<KioraRestaurante.Services.Cupons.CupomService>();

// Serviços de notificações.
builder.Services.AddScoped<KioraRestaurante.Services.Notificacoes.NotificacaoService>();
builder.Services.AddScoped<KioraRestaurante.Services.Notificacoes.NotificacaoDispatcher>();
builder.Services.AddHostedService<KioraRestaurante.Services.Notificacoes.DistribuirNotificacoesService>();

// Relatórios e gerenciamento de pedidos.
builder.Services.AddScoped<RelatoriosAdminService>();
builder.Services.AddScoped<GerenciarPedidoService>();

// Serviços de salão e atendimento.
builder.Services.AddScoped<KioraRestaurante.Services.Salao.AcessoSalaoService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.ComandoSalaoService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.ComandaService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.ConsultaSalaoService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.PortariaService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.AvisosSalaoService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.CozinhaService>();
builder.Services.AddScoped<KioraRestaurante.Services.Salao.FuncionarioService>();

// Serviços de andamento e operações de pedidos.
builder.Services.AddHttpClient<EmailService>(
    http => http.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddScoped<KioraRestaurante.Services.Pedidos.AndamentoPedidoService>();
builder.Services.AddHostedService<OperacoesPedidoService>();

// As cotas são compartilhadas pelas APIs e pelos formulários que executam a mesma operação.
builder.Services.AddLimitesKiora();

// Configurações de entrega.
builder.Services.AddOptions<KioraRestaurante.Options.EntregaOptions>()
    .Configure(o => o.Faixas = [])
    .Bind(builder.Configuration.GetSection("Entrega"))
    .PostConfigure(o =>
    {
        if (o.Faixas.Length == 0)
            o.Faixas = new KioraRestaurante.Options.EntregaOptions().Faixas;
    })
    .Validate(o =>
        o.LimiteMetros == 35000 &&
        o.Faixas.Length > 0 &&
        o.Faixas.All(f =>
            f.AteMetros > 0 &&
            f.Valor is >= 0 and <= 99999999.99m &&
            decimal.Round(f.Valor, 2) == f.Valor) &&
        o.Faixas.Select(f => f.AteMetros).Distinct().Count() == o.Faixas.Length &&
        o.Faixas.Max(f => f.AteMetros) == o.LimiteMetros,
        "Confira a área e a tabela de entrega.")
    .ValidateOnStart();

builder.Services.AddScoped<ValidarSessaoUsuario>();

// Limita tentativas de login.
builder.Services.AddSingleton(_ => new LimitadorLogin(
    builder.Configuration.GetValue<bool>("Seguranca:LimitarLoginPorIp")));

builder.Services.AddScoped<LimitarTentativasLogin>();

// Limita tentativas de recuperação de senha.
builder.Services.AddSingleton(_ => new LimitadorRecuperacaoSenha(
    builder.Configuration.GetValue<bool>("Seguranca:LimitarLoginPorIp")));

builder.Services.AddScoped<LimitarRecuperacaoSenha>();
builder.Services.AddScoped<EnderecoUsuarioService>();

// Proteção antifalsificação.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Cache em memória.
builder.Services.AddMemoryCache();

// ======================================================
// SERVIÇO DE CEP
// ======================================================

builder.Services.AddHttpClient<CepService>(http =>
{
    http.BaseAddress = new Uri("https://viacep.com.br/ws/");
    http.Timeout = TimeSpan.FromSeconds(8);
});

// ======================================================
// CONFIGURAÇÕES E SERVIÇOS DE RESERVAS
// ======================================================

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Serviço responsável por identificar os produtos mais pedidos.
builder.Services.AddScoped<KioraRestaurante.Services.Cardapio.MaisPedidosService>();

// Configuração do Mercado Pago.
builder.Services.Configure<KioraRestaurante.Options.MercadoPagoConfig>(
    builder.Configuration.GetSection("MercadoPago"));

// Cliente HTTP para comunicação com o Mercado Pago.
builder.Services.AddHttpClient<KioraRestaurante.Services.PagamentosOnline.MpOrdersClient>(http =>
{
    http.BaseAddress = new Uri("https://api.mercadopago.com/");
    http.Timeout = TimeSpan.FromSeconds(10);
});

// Serviço de checkout dos pagamentos online.
builder.Services.AddScoped<KioraRestaurante.Services.PagamentosOnline.CheckoutOnlineService>();

// Serviço de conferência dos pagamentos online.
builder.Services.AddHostedService<KioraRestaurante.Services.PagamentosOnline.ConferenciaOnlineService>();

builder.Services.AddOptions<KioraRestaurante.Options.ReservaOptions>()
    .Bind(builder.Configuration.GetSection("Reservas"))
    .Validate(o =>
        o.AberturaMinutos >= 0 &&
        o.AberturaMinutos < o.UltimaReservaMinutos &&
        o.UltimaReservaMinutos < o.FechamentoMinutos &&
        o.FechamentoMinutos <= 1440 &&
        o.IntervaloMinutos > 0 &&
        o.PermanenciaMinutos > 0 &&
        o.ToleranciaMinutos > 0 &&
        o.ChamadaMinutos > 0 &&
        o.AntecedenciaDias is > 0 and <= 90,
        "Configuração de reservas inválida.")
    .ValidateOnStart();

// Serviço de cotação de entrega.
builder.Services.AddScoped<KioraRestaurante.Services.Entrega.CotacaoEntregaService>();

// Integração com o serviço de cálculo de rotas.
builder.Services.AddHttpClient<
    KioraRestaurante.Services.Entrega.IRotasEntrega,
    KioraRestaurante.Services.Entrega.HeiGitRotasEntrega>(http =>
    {
        http.BaseAddress = new Uri("https://api.heigit.org/");
        http.Timeout = TimeSpan.FromSeconds(15);
        http.MaxResponseContentBufferSize = 1_000_000;
    })
.ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler { AllowAutoRedirect = false })
.RemoveAllLoggers();

// Integração com o serviço de geocodificação.
builder.Services.AddHttpClient<
    KioraRestaurante.Services.Entrega.IGeocodificadorEntrega,
    KioraRestaurante.Services.Entrega.GeoapifyGeocodificadorEntrega>(http =>
    {
        http.BaseAddress = new Uri("https://api.geoapify.com/");
        http.Timeout = TimeSpan.FromSeconds(15);
        http.MaxResponseContentBufferSize = 1_000_000;
    })
.ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler { AllowAutoRedirect = false })
.RemoveAllLoggers();

builder.Services.AddScoped<KioraRestaurante.Services.Reservas.HorariosReserva>();
builder.Services.AddScoped<KioraRestaurante.Services.Reservas.IdentidadeReserva>();
builder.Services.AddScoped<KioraRestaurante.Services.Reservas.ReservaService>();

builder.Services.AddHostedService<KioraRestaurante.Services.Reservas.ExpiracaoReservasService>();
builder.Services.AddHostedService<KioraRestaurante.Services.Imagens.LimpezaImagensService>();

// ======================================================
// INICIALIZAÇÃO DA APLICAÇÃO
// ======================================================

var app = builder.Build();

// Swagger somente em desenvolvimento.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRespostasApiKiora();

// Configurações de produção.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Autenticação e autorização.
app.UseAuthentication();
app.UseAuthorization();

// Aplica os limites de requisições configurados.
app.UseRateLimiter();

app.MapStaticAssets();

// Compatibilidade com endereços antigos.
app.MapControllerRoute(
    name: "conta-legada",
    pattern: "Account/{action=Login}/{id?}",
    defaults: new { controller = "Conta" })
    .WithMetadata(new Microsoft.AspNetCore.Routing.SuppressLinkGenerationMetadata());

// Rota padrão.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Endpoints com atributos de rota.
app.MapControllers();

// Inicia o servidor.
app.Run();
