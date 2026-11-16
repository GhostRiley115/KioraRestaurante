
using KioraRestaurante.Data;
using KioraRestaurante.Filters;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configura a documentação da API para aceitar autenticação JWT.
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Informe o token JWT para autenticar as requisições."
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// Pega a string de conexão do arquivo appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// Descobre a versão do MySQL uma vez durante a inicialização.
var versaoMySql = ServerVersion.AutoDetect(connectionString);

// Configura o contexto do banco de dados.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, versaoMySql));

// ======================================================
// AUTENTICAÇÃO POR COOKIE E JWT
// ======================================================

builder.Services.AddAuthentication(options =>
{
    // Seleciona automaticamente Cookie ou JWT.
    options.DefaultScheme = "CookieOuJwt";
    options.DefaultAuthenticateScheme = "CookieOuJwt";
    options.DefaultChallengeScheme = "CookieOuJwt";
    options.DefaultForbidScheme = "CookieOuJwt";

    // Login e logout do site continuam utilizando Cookies.
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddPolicyScheme("CookieOuJwt", "Cookie ou JWT", options =>
{
    // Se houver Bearer no cabeçalho, utiliza JWT.
    // Caso contrário, utiliza Cookie.
    options.ForwardDefaultSelector = context =>
    {
        var authorization = context.Request.Headers.Authorization.ToString();

        return authorization.StartsWith(
            "Bearer ",
            StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(options =>
{
    options.LoginPath = "/Conta/Login";
    options.LogoutPath = "/Conta/Logout";
    options.AccessDeniedPath = "/Home/AcessoNegado";

    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    // Verifica se a conta continua ativa.
    options.EventsType = typeof(ValidarSessaoUsuario);
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Recupera a chave secreta armazenada no User Secrets.
    var chaveSecreta = builder.Configuration["Jwt:ChaveSecreta"];

    if (string.IsNullOrWhiteSpace(chaveSecreta))
    {
        throw new InvalidOperationException(
            "A chave secreta do JWT não foi configurada.");
    }

    // Define as regras de validação do JWT.
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(chaveSecreta)),

        ValidateIssuer = true,
        ValidIssuer = "KioraRestauranteAPI",

        ValidateAudience = true,
        ValidAudience = "AppKiora",

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // Verifica se o usuário e a sessão continuam válidos.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var id = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var versao = context.Principal?
                .FindFirst("VersaoSessao")?.Value;

            // Verifica as informações obrigatórias do token.
            if (!int.TryParse(id, out var usuarioId) ||
                !Guid.TryParse(versao, out var versaoSessao) ||
                versaoSessao == Guid.Empty)
            {
                context.Fail("Token inválido.");
                return;
            }

            // Recupera o serviço de usuários.
            var usuarioService = context.HttpContext.RequestServices
                .GetRequiredService<IUsuarioService>();

            // Busca o usuário ativo.
            var usuario = usuarioService.BuscarPorId(usuarioId);

            var tipoToken = context.Principal?
                .FindFirst(ClaimTypes.Role)?.Value;

            // Confere conta, sessão e perfil.
            if (usuario == null ||
                usuario.VersaoSessao != versaoSessao ||
                usuario.Tipo.ToString() != tipoToken)
            {
                context.Fail("Sessão inválida ou expirada.");
            }
        }
    };
});

// ======================================================
// CONTROLLERS E VIEWS
// ======================================================

builder.Services.AddControllersWithViews();

// ======================================================
// INJEÇÃO DE DEPENDÊNCIAS
// ======================================================

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddHttpClient<IEmailService, EmailService>(
    http => http.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddDataProtection();
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

// Limita a quantidade de requisições por usuário.
builder.Services.AddRateLimiter(limites =>
{
    limites.AddPolicy("salao", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? contexto.Connection.RemoteIpAddress?.ToString()
                ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    limites.AddPolicy("convite-funcionario", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    limites.AddPolicy("cotacao-entrega", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? "sem-sessao",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    limites.AddPolicy("alteracoes-pedido", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? "sem-sessao",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    limites.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensagem = "Aguarde um minuto antes de tentar novamente." },
            ct);
    };
});

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
