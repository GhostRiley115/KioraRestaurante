using System.Reflection;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;
namespace KioraRestaurante.Tests;
public class SalaoPermissoesApiTests
{
    [Theory]
    [InlineData(typeof(SalaoApiController))]
    [InlineData(typeof(FuncionariosApiController))]
    [InlineData(typeof(EquipeApiController))]
    public void ApiUsaProtecaoContraCsrfECachePrivado(Type controller)
    {
        Assert.NotNull(controller.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>());
        Assert.True(controller.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }
    [Fact] public void LoginDoAplicativoPreservaLimitacaoDeTentativasELeFormulario()
    {
        var metodo=typeof(ContaController).GetMethod(nameof(ContaController.LoginAplicativo))!;
        Assert.Equal("/api/equipe/login",metodo.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Contains(metodo.GetCustomAttributes<ServiceFilterAttribute>(),a=>a.ServiceType==typeof(KioraRestaurante.Filters.LimitarTentativasLogin));
        Assert.NotNull(metodo.GetParameters()[0].GetCustomAttribute<FromFormAttribute>());
    }
    [Fact] public async Task DetalhesDeAvisoNaoPermiteIdDeOutraPessoaOuFuncao()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);await b.PrepararItem(id);
        var avisos=new KioraRestaurante.Services.Notificacoes.NotificacaoService(b.Db,new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(),b.Relogio);
        var aviso=Assert.Single((await avisos.Listar(b.Garcom.Id,1)).Itens);
        Assert.True((await avisos.Detalhe(b.Garcom.Id,aviso.Id)).NaoLida);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>avisos.Detalhe(b.OutroGarcom.Id,aviso.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>avisos.Detalhe(b.Cliente.Id,aviso.Id));
        Assert.Equal(1,await avisos.NaoLidas(b.Garcom.Id));
    }
    [Fact] public async Task AcessoRevogadoDepoisDaConsultaNaoConfirmaComandoPendente()
    {
        using var b=new BancoSalao();await b.Acesso.Exigir(b.Garcom.Id,default,KioraRestaurante.Models.Enums.TipoUsuario.Garcom);
        await b.Db.Usuarios.Where(u=>u.Id==b.Garcom.Id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Ativo,false));
        var executada=false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Comandos.Executar(b.Garcom.Id,"teste",Guid.NewGuid(),new{},()=>
        {executada=true;return Task.FromResult(new KioraRestaurante.Services.Salao.ResultadoComando(null,null,"Não confirmar"));},default));
        Assert.False(executada);Assert.Empty(await b.Db.MovimentosSalao.ToListAsync());
    }
}
