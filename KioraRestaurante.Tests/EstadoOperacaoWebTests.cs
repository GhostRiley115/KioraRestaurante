using System.Security.Claims;
using KioraRestaurante.Controllers;
using KioraRestaurante.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KioraRestaurante.Tests;
public class EstadoOperacaoWebTests
{
    private static RestauranteController Controller(BancoSalao b)
    {
        var http=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,b.Admin.Id.ToString())],"teste"))};
        return new(b.Consulta,b.Comandas,b.PortariaService,b.Cozinha,b.Acesso,null!,b.Horarios,null!){ControllerContext=new(){HttpContext=http},TempData=new TempDataDictionary(http,new MemoriaTempData())};
    }
    [Theory] [InlineData("salao","NaFila","salao","NaFila")] [InlineData("https://externo.example","invalido","todas","todas")]
    public async Task MudancaDePreparoMantemFiltrosValidosSemAceitarValoresArbitrarios(string origem,string etapa,string esperadoOrigem,string esperadoEtapa)
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);var item=Assert.Single(await b.Cozinha.Fila(b.Admin.Id));
        var resultado=Assert.IsType<RedirectToActionResult>(await Controller(b).Preparo(new(){Origem="salao",ItemSalaoId=item.ItemSalaoId,Versao=item.Versao,Acao="iniciar",ChaveSolicitacao=Guid.NewGuid()},origem,etapa));
        Assert.Equal("Cozinha",resultado.ActionName);Assert.Equal(esperadoOrigem,resultado.RouteValues!["origem"]);Assert.Equal(esperadoEtapa,resultado.RouteValues["etapa"]);
    }
    [Fact] public async Task CancelarReservaDeOutroDiaPreservaAConsultaEmFormatoIso()
    {
        using var b=new BancoSalao();var r=new Reserva{Nome="Grupo futuro",Pessoas=2,Telefone="11900000000",ChaveSolicitacao=Guid.NewGuid(),InicioUtc=b.Relogio.Agora.AddDays(1).UtcDateTime,Situacao=SituacaoReserva.AguardandoChegada};b.Db.Add(r);await b.Db.SaveChangesAsync();
        var dia=new DateOnly(2026,11,8);var resultado=Assert.IsType<RedirectToActionResult>(await Controller(b).CancelarReserva(new(){Codigo=r.Codigo,Versao=r.Versao,ChaveSolicitacao=Guid.NewGuid(),Motivo="Solicitação de teste"},dia));
        Assert.Equal("Portaria",resultado.ActionName);Assert.Equal("2026-11-08",resultado.RouteValues!["data"]);Assert.Equal(SituacaoReserva.Cancelada,(await b.Db.Reservas.SingleAsync()).Situacao);
    }
    private sealed class MemoriaTempData:ITempDataProvider
    {
        public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();
        public void SaveTempData(HttpContext context,IDictionary<string,object> values){}
    }
}
