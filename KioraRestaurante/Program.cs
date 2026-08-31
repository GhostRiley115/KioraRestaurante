using KioraRestaurante.Data;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// pega a string de conxão do arquivo appsettings.json
var connectionString = builder.Configuration.GetConnectionString("ConexaoNuvem");

// avisa ao sistema para utilizar o myqsl
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Add services to the container.
builder.Services.AddControllersWithViews();

// Registra o UsuarioService para injeção de dependência
builder.Services.AddScoped<IUsuarioServices, UsuarioServices>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
