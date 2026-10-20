using KioraRestaurante.Data;
using KioraRestaurante.Filters;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configura a documentação da API para aceitar autenticação JWT.

builder.Services.AddSwaggerGen(options =>
{
    // Define a autenticação por token JWT.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Informe o token JWT para autenticar as requisições."
    });

    // Permite utilizar o token nas requisições da API.
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});


// Pega a string de conexão do arquivo appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// Descobre a versão do MySQL uma vez durante a inicialização.
// Evita repetir essa conexão ao configurar novos contextos.
var versaoMySql = ServerVersion.AutoDetect(connectionString);

// Os contextos reutilizam a informação já descoberta.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, versaoMySql));

// AUTENTICAÇÃO POR COOKIE E JWT
// Mantém os Cookies como padrão para o site.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        // Define para onde o usuário será enviado
        // caso tente acessar uma área protegida sem estar logado.
        options.LoginPath = "/Conta/Login";

        // Define o caminho utilizado para sair da conta.
        options.LogoutPath = "/Conta/Logout";
        // Clientes que tentam acessar a administração recebem uma página explicativa.
        options.AccessDeniedPath = "/Home/AcessoNegado";

        // Define o tempo de validade do Cookie.
        options.ExpireTimeSpan = TimeSpan.FromHours(2);

        // Renova automaticamente o Cookie enquanto o usuário
        // continuar utilizando o sistema.
        options.SlidingExpiration = true;

        // Confere se a conta continua ativa em cada requisição autenticada.
        options.EventsType = typeof(ValidarSessaoUsuario);
    })

    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Recupera a chave secreta armazenada no User Secrets.
        var chaveSecreta = builder.Configuration["Jwt:ChaveSecreta"];

        // Impede a inicialização caso a chave não esteja configurada.
        if (string.IsNullOrWhiteSpace(chaveSecreta))
        {
            throw new InvalidOperationException(
                "A chave secreta do JWT não foi configurada.");
        }

        // Define como os tokens JWT serão validados.
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
    });


// Adiciona suporte aos Controllers e às Views do ASP.NET Core MVC.
builder.Services.AddControllersWithViews();

// INJEÇÃO DE DEPENDÊNCIA
// Registra o UsuarioService para que o ASP.NET Core
// possa fornecer automaticamente uma instância dele
// para os Controllers que precisarem do serviço.
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddHttpClient<IEmailService, EmailService>(http => http.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddDataProtection();
builder.Services.AddScoped<CarrinhoCookie>();
builder.Services.AddHostedService<LimpezaCarrinhosService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IImagemProdutoService, ImagemProdutoService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ValidarSessaoUsuario>();
// Limita o login por e-mail e, opcionalmente, por IP antes de executar a autenticação.
builder.Services.AddSingleton(_ => new LimitadorLogin(
    builder.Configuration.GetValue<bool>("Seguranca:LimitarLoginPorIp")));
builder.Services.AddScoped<LimitarTentativasLogin>();
// Envio e conferência de códigos têm limites próprios, sem consumir as tentativas de login.
builder.Services.AddSingleton(_ => new LimitadorRecuperacaoSenha(
    builder.Configuration.GetValue<bool>("Seguranca:LimitarLoginPorIp")));
builder.Services.AddScoped<LimitarRecuperacaoSenha>();
builder.Services.AddScoped<EnderecoUsuarioService>();
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-CSRF-TOKEN"; });
// Cache em memória para consultas públicas de CEP.
builder.Services.AddMemoryCache();

// HttpClient gerenciado pelo ASP.NET.
builder.Services.AddHttpClient<CepService>(http =>
{
    http.BaseAddress = new Uri("https://viacep.com.br/ws/");
    http.Timeout = TimeSpan.FromSeconds(8);
});


var app = builder.Build();

// Disponibiliza o Swagger somente durante o desenvolvimento.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Verifica se a aplicação não está em ambiente de desenvolvimento.
if (!app.Environment.IsDevelopment())
{
    // Utiliza uma página de erro personalizada.
    app.UseExceptionHandler("/Home/Error");

    // Ativa o HSTS para aumentar a segurança da aplicação.
    app.UseHsts();
}

// Redireciona requisições HTTP para HTTPS.
app.UseHttpsRedirection();

// Permite servir arquivos da pasta wwwroot,
// omo CSS, JavaScript e imagens.
app.UseStaticFiles();

// Ativa o sistema de roteamento.
app.UseRouting();

// Verifica o Cookie de autenticação e identifica
// se existe um usuário conectado.
app.UseAuthentication();


// verificar se o usuário possui autorização
// para acessar determinadas áreas da aplicação.
app.UseAuthorization();

// Mapeia os arquivos estáticos da aplicação.
app.MapStaticAssets();

// Compatibilidade com endereços antigos, sem gerar novos links com Account.
// A rota aponta para o mesmo controller e preserva os métodos GET e POST.
app.MapControllerRoute(
    name: "conta-legada",
    pattern: "Account/{action=Login}/{id?}",
    defaults: new { controller = "Conta" })
    .WithMetadata(new Microsoft.AspNetCore.Routing.SuppressLinkGenerationMetadata());

// Define a rota padrão da aplicação.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Mapeia os endpoints que utilizam atributos de rota.
app.MapControllers();

// INICIA A APLICAÇÃO
// Inicia o servidor.
app.Run();
