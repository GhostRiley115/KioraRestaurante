using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Pega a string de conexão do arquivo appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// Descobre a versão do MySQL uma vez durante a inicialização.
// Evita repetir essa conexão ao configurar novos contextos.
var versaoMySql = ServerVersion.AutoDetect(connectionString);

// Os contextos reutilizam a informação já descoberta.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, versaoMySql));

// AUTENTICAÇÃO POR COOKIE
// Configura o sistema de autenticação do ASP.NET Core.
// O Cookie será utilizado para manter o usuário autenticado
// depois que ele realizar o login.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
    });

// Adiciona suporte aos Controllers e às Views do ASP.NET Core MVC.
builder.Services.AddControllersWithViews();

// INJEÇÃO DE DEPENDÊNCIA
// Registra o UsuarioService para que o ASP.NET Core
// possa fornecer automaticamente uma instância dele
// para os Controllers que precisarem do serviço.
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddDataProtection();
builder.Services.AddScoped<CarrinhoCookie>();
builder.Services.AddHostedService<LimpezaCarrinhosService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IImagemProdutoService, ImagemProdutoService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ValidarSessaoUsuario>();
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

// INICIA A APLICAÇÃO
// Inicia o servidor.
app.Run();