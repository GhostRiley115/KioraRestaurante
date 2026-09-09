using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Pega a string de conexão do arquivo appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// Configura o Entity Framework para utilizar o MySQL.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    ));

//AUTENTICAÇÃO POR COOKIE
//Configura o sistema de autenticação do ASP.NET Core.
//O Cookie será utilizado para manter o usuário autenticado
//depois que ele realizar o login.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        //Define para onde o usuário será enviado
        //caso tente acessar uma área protegida sem estar logado.
        options.LoginPath = "/Account/Login";

        //Define o caminho utilizado para sair da conta.
        options.LogoutPath = "/Account/Logout";

        //Define o tempo de validade do Cookie.
        options.ExpireTimeSpan = TimeSpan.FromHours(2);

        //Renova automaticamente o Cookie enquanto o usuário
        //continuar utilizando o sistema.
        options.SlidingExpiration = true;
    });

//Adiciona suporte aos Controllers e às Views do ASP.NET Core MVC.
builder.Services.AddControllersWithViews();

//INJEÇÃO DE DEPENDÊNCIA
//Registra o UsuarioService para que o ASP.NET Core
//possa fornecer automaticamente uma instância dele
//para os Controllers que precisarem do serviço.
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

var app = builder.Build();

//Verifica se a aplicação não está em ambiente de desenvolvimento.
if (!app.Environment.IsDevelopment())
{
    //Utiliza uma página de erro personalizada.
    app.UseExceptionHandler("/Home/Error");

    //Ativa o HSTS para aumentar a segurança da aplicação.
    app.UseHsts();
}

//Redireciona requisições HTTP para HTTPS.
app.UseHttpsRedirection();

//Permite servir arquivos da pasta wwwroot,
//omo CSS, JavaScript e imagens.
app.UseStaticFiles();

//Ativa o sistema de roteamento.
app.UseRouting();

//Verifica o Cookie de autenticação e identifica
//se existe um usuário conectado.
app.UseAuthentication();


//verificar se o usuário possui autorização
//para acessar determinadas áreas da aplicação.
app.UseAuthorization();

//Mapeia os arquivos estáticos da aplicação.
app.MapStaticAssets();

//Define a rota padrão da aplicação.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

//INICIA A APLICAÇÃO
//Inicia o servidor.
app.Run();