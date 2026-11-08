using System.Security.Claims;
using System.Text.RegularExpressions;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace KioraRestaurante.Tests;
public class GerenciamentoRazorTests
{
    [Theory]
    [InlineData("Administrador",true,true,true,true,true)]
    [InlineData("Gerente",true,true,true,true,false)]
    [InlineData("Garcom",true,true,false,false,false)]
    [InlineData("Portaria",false,true,false,false,false)]
    [InlineData("Cozinha",false,false,true,false,false)]
    public async Task MenuCompartilhadoExibeSomenteFuncoesDoCargo(string cargo,bool mesas,bool recepcao,bool cozinha,bool equipe,bool geral)
    {
        await using var telas=new Telas();var html=await telas.Render("~/Views/Shared/_MenuGerenciamento.cshtml",new object(),cargo);
        Assert.Equal(mesas,html.Contains(">Salão e mesas</a>"));Assert.Equal(recepcao,html.Contains(">Reservas e recepção</a>"));Assert.Equal(cozinha,html.Contains(">Fila da cozinha</a>"));Assert.Equal(equipe,html.Contains(">Equipe e permissões</a>"));Assert.Equal(geral,html.Contains(">Visão geral</a>"));Assert.Equal(geral,html.Contains(">Cupons e parceiros</a>"));
    }
    [Fact] public async Task ListaDeCuponsFicaVisivelSemAbrirExplicacaoDasRegras()
    {
        await using var telas=new Telas();var html=await telas.Render("~/Views/Admin/Cupons/Index.cshtml",new List<CupomResumoAdminDTO>{new(){Id=1,Codigo="BLACKFRIDAY",LimitePorCliente=null,FimEmUtc=new(2026,12,1)}},"Administrador");
        var ajuda=Regex.Match(html,@"<details\b[\s\S]*?</details>");Assert.True(ajuda.Success);Assert.DoesNotContain("<table",ajuda.Value);Assert.Contains("BLACKFRIDAY",html[(ajuda.Index+ajuda.Length)..]);Assert.Contains("Reutiliz",html[(ajuda.Index+ajuda.Length)..]);
    }
    [Fact] public async Task FiltroDaVisaoGeralPreservaDatasDaAnaliseEComparacao()
    {
        await using var telas=new Telas();var html=await telas.Render("~/Views/Admin/_FiltrosRelatorio.cshtml",new FiltroRelatorioAdmin{Inicio=new(2026,11,1),Fim=new(2026,11,8),CompararInicio=new(2026,10,1),CompararFim=new(2026,10,8)},"Administrador","Index");
        Assert.Contains("name=\"Inicio\"",html);Assert.Contains("name=\"CompararInicio\"",html);Assert.Contains("method=\"get\"",html);Assert.Contains("value=\"2026-11-08\"",html);
    }
    private sealed class Telas:IAsyncDisposable
    {
        private readonly WebApplication _app;
        public Telas()
        {
            var b=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(AdminService).Assembly.GetName().Name,ContentRootPath=Path.GetTempPath(),EnvironmentName="Testing"});
            b.Services.AddDataProtection().UseEphemeralDataProtectionProvider();b.Services.AddSingleton<TimeProvider>(TimeProvider.System);b.Services.AddControllersWithViews().AddApplicationPart(typeof(AdminService).Assembly);_app=b.Build();_app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        }
        public async Task<string> Render<T>(string caminho,T model,string cargo,string? acaoFiltro=null)
        {
            using var scope=_app.Services.CreateScope();var http=new DefaultHttpContext{RequestServices=scope.ServiceProvider,User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,cargo)],"teste"))};http.Request.Scheme="https";http.Request.Host=new("localhost");http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(),"Teste Razor"));
            var route=new RouteData();route.Values["controller"]="Admin";route.Values["action"]="Index";
            var action=new ActionContext(http,route,new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());var view=scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null,caminho,false);Assert.True(view.Success);
            var dados=new ViewDataDictionary<T>(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};if(acaoFiltro!=null)dados["AcaoRelatorio"]=acaoFiltro;
            var temp=new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>());await using var writer=new StringWriter();await view.View.RenderAsync(new ViewContext(action,view.View,dados,temp,writer,new HtmlHelperOptions()));return writer.ToString();
        }
        public ValueTask DisposeAsync()=>_app.DisposeAsync();
    }
}
