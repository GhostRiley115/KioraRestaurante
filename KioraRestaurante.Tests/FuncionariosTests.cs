using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KioraRestaurante.Tests;

public class FuncionariosTests
{
    private static SalvarFuncionarioDTO Novo()=>new(){Nome="Nova Pessoa",Email="nova@example.com",Telefone="(11) 90000-1234",Cargo=TipoUsuario.Garcom};
    private static string Token(BancoSalao b)
    {
        using var json=JsonDocument.Parse(b.Emails.Corpos.Last());var html=json.RootElement.GetProperty("htmlContent").GetString()!;
        return Regex.Match(WebUtility.HtmlDecode(html),@"token=([A-F0-9]{64})").Groups[1].Value;
    }
    [Fact] public async Task ConviteUsaHashExpiraEFuncionarioDefineSenhaUmaUnicaVez()
    {
        using var b=new BancoSalao();var resultado=await b.Funcionarios.Salvar(b.Gerente.Id,Novo());Assert.True(resultado.ConviteEnviado);
        var token=Token(b);Assert.Equal(64,token.Length);var usuario=await b.Db.Usuarios.SingleAsync(u=>u.Id==resultado.Id);
        Assert.True(usuario.ConvitePendente);Assert.Equal("11900001234",usuario.TelefoneFuncionario);
        var convite=await b.Db.ConvitesFuncionarios.SingleAsync();Assert.NotEqual(token,convite.TokenHash);Assert.NotEqual(token,usuario.SenhaHash);
        var antes=usuario.VersaoSessao;Assert.True(await b.Funcionarios.ConferirConvite(token));
        await b.Funcionarios.Aceitar(new(){Token=token,Senha="Senha de teste 123",ConfirmarSenha="Senha de teste 123"});
        Assert.False(usuario.ConvitePendente);Assert.NotEqual(antes,usuario.VersaoSessao);Assert.False(await b.Funcionarios.ConferirConvite(token));
        Assert.NotEqual(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,new Microsoft.AspNetCore.Identity.PasswordHasher<KioraRestaurante.Models.Usuario>().VerifyHashedPassword(usuario,usuario.SenhaHash,"Senha de teste 123"));
        await Assert.ThrowsAsync<ValidationException>(()=>b.Funcionarios.Aceitar(new(){Token=token,Senha="Outra senha 123",ConfirmarSenha="Outra senha 123"}));
    }
    [Fact] public async Task ReenviarInvalidaLinkAnteriorEExpiracaoEConferidaNoServidor()
    {
        using var b=new BancoSalao();var resultado=await b.Funcionarios.Salvar(b.Admin.Id,Novo());var tokenAntigo=Token(b);
        var usuario=await b.Db.Usuarios.SingleAsync(u=>u.Id==resultado.Id);
        await b.Funcionarios.Reenviar(b.Admin.Id,usuario.Id,usuario.VersaoFuncionario);var token=Token(b);
        Assert.False(await b.Funcionarios.ConferirConvite(tokenAntigo));Assert.True(await b.Funcionarios.ConferirConvite(token));
        b.Relogio.Agora=b.Relogio.Agora.AddHours(25);Assert.False(await b.Funcionarios.ConferirConvite(token));
    }
    [Fact] public async Task GerenteNaoPodePromoverAAdministradorNemGerenciarOutroGerente()
    {
        using var b=new BancoSalao();var dto=Novo();dto.Cargo=TipoUsuario.Administrador;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Funcionarios.Salvar(b.Gerente.Id,dto));
        dto.Cargo=TipoUsuario.Gerente;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Funcionarios.Salvar(b.Gerente.Id,dto));
        Assert.True((await b.Funcionarios.Salvar(b.Admin.Id,dto)).ConviteEnviado);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Funcionarios.Edicao(b.Gerente.Id,b.Gerente.Id));
    }
    [Theory] [InlineData(TipoUsuario.Garcom)] [InlineData(TipoUsuario.Portaria)] [InlineData(TipoUsuario.Cozinha)] [InlineData(TipoUsuario.Cliente)]
    public async Task CargoOperacionalNaoCadastraFuncionarios(TipoUsuario cargo)
    {using var b=new BancoSalao();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Funcionarios.Salvar(b.Usuarios.First(u=>u.Tipo==cargo).Id,Novo()));}
    [Fact] public async Task DesativarOuMudarCargoExigeResolverMesasERevogaSessaoAnterior()
    {
        using var b=new BancoSalao();var id=await b.Abrir();var dto=await b.Funcionarios.Edicao(b.Gerente.Id,b.Garcom.Id);dto.Ativo=false;
        await Assert.ThrowsAsync<ValidationException>(()=>b.Funcionarios.Salvar(b.Gerente.Id,dto));
        var a=await b.Detalhe(id);await b.Comandas.Transferir(b.Gerente.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),GarcomId=b.OutroGarcom.Id});
        var antes=b.Garcom.VersaoSessao;await b.Funcionarios.Salvar(b.Gerente.Id,dto);b.Db.ChangeTracker.Clear();
        var usuario=await b.Db.Usuarios.SingleAsync(u=>u.Id==b.Garcom.Id);Assert.False(usuario.Ativo);Assert.NotEqual(antes,usuario.VersaoSessao);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Consulta.Mesas(b.Garcom.Id));
    }
    [Fact] public async Task EmailDeClienteNaoPodeSerConvertidoEmContaDeFuncionario()
    {
        using var b=new BancoSalao();var dto=Novo();dto.Email=b.Cliente.Email;
        await Assert.ThrowsAsync<ValidationException>(()=>b.Funcionarios.Salvar(b.Admin.Id,dto));Assert.Equal(TipoUsuario.Cliente,(await b.Db.Usuarios.SingleAsync(u=>u.Id==b.Cliente.Id)).Tipo);
    }
    [Fact] public async Task FalhaDoEmailMantemConvitePendenteSemExporSenhaPermiteReenvio()
    {
        using var b=new BancoSalao();b.Emails.Falhar=true;var resultado=await b.Funcionarios.Salvar(b.Admin.Id,Novo());
        Assert.False(resultado.ConviteEnviado);var u=await b.Db.Usuarios.SingleAsync(u=>u.Id==resultado.Id);Assert.True(u.ConvitePendente);
        b.Emails.Falhar=false;Assert.True(await b.Funcionarios.Reenviar(b.Admin.Id,u.Id,u.VersaoFuncionario));
    }
}
