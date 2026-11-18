using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class EstadoMesaApiTests
{
    [Fact]
    public void CorpoVazioNaoSignificaDesabilitarMesa()
    {
        var dto=JsonSerializer.Deserialize<AlterarMesaReservaDTO>("{}")!;
        var erros=new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto,new(dto),erros,true));
        Assert.Null(dto.Ativa);
        Assert.Contains(erros,e=>e.MemberNames.Contains(nameof(dto.Ativa)));
    }

    [Theory]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public void AlteracaoExigeVersaoEChaveNaoVazias(bool semVersao,bool semChave)
    {
        var dto=new AlterarMesaReservaDTO{Ativa=false,Versao=semVersao?Guid.Empty:Guid.NewGuid(),ChaveSolicitacao=semChave?Guid.Empty:Guid.NewGuid()};
        Assert.Throws<ValidationException>(()=>Validator.ValidateObject(dto,new(dto),true));
    }

    [Theory]
    [InlineData(TipoUsuario.Administrador)]
    [InlineData(TipoUsuario.Gerente)]
    public async Task AliasPreservaConfiguracaoEReenvioNaoDuplicaHistorico(TipoUsuario cargo)
    {
        using var b=new BancoSalao();var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        var nome=mesa.Nome;var capacidade=mesa.Capacidade;var versao=mesa.Versao;
        var dto=new AlterarMesaReservaDTO{Ativa=false,Versao=versao,ChaveSolicitacao=Guid.NewGuid()};
        var usuario=cargo==TipoUsuario.Administrador?b.Admin:b.Gerente;
        await b.PortariaService.AlterarHabilitacaoMesa(usuario.Id,mesa.Id,dto);
        await b.PortariaService.AlterarHabilitacaoMesa(usuario.Id,mesa.Id,dto);
        mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        Assert.False(mesa.Ativa);Assert.Equal(nome,mesa.Nome);Assert.Equal(capacidade,mesa.Capacidade);Assert.NotEqual(versao,mesa.Versao);
        var registro=Assert.Single(await b.Db.MovimentosSalao.ToListAsync());
        Assert.Equal(usuario.Id,registro.UsuarioId);Assert.Equal(dto.ChaveSolicitacao,registro.Id);Assert.Equal("habilitar-mesa",registro.Acao);
    }

    [Fact]
    public async Task CelularComVersaoAntigaNaoDesfazConfiguracaoMaisNova()
    {
        using var b=new BancoSalao();var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);var versaoAntiga=mesa.Versao;
        await b.PortariaService.ConfigurarMesa(b.Gerente.Id,new(){MesaId=mesa.Id,Nome="Mesa revisada",Capacidade=4,Ativa=false,Versao=versaoAntiga,ChaveSolicitacao=Guid.NewGuid()});
        await Assert.ThrowsAsync<ValidationException>(()=>b.PortariaService.AlterarHabilitacaoMesa(b.Admin.Id,1,new(){Ativa=true,Versao=versaoAntiga,ChaveSolicitacao=Guid.NewGuid()}));
        mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        Assert.False(mesa.Ativa);Assert.Equal("Mesa revisada",mesa.Nome);Assert.Equal(4,mesa.Capacidade);
        Assert.Single(await b.Db.MovimentosSalao.ToListAsync());
    }

    [Fact]
    public async Task ChaveJaUtilizadaNaoPodeSerReaproveitadaComOutroEstado()
    {
        using var b=new BancoSalao();var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        var dto=new AlterarMesaReservaDTO{Ativa=false,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid()};
        await b.PortariaService.AlterarHabilitacaoMesa(b.Admin.Id,1,dto);
        dto.Ativa=true;
        await Assert.ThrowsAsync<ValidationException>(()=>b.PortariaService.AlterarHabilitacaoMesa(b.Admin.Id,1,dto));
        Assert.False((await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1)).Ativa);
        Assert.Single(await b.Db.MovimentosSalao.ToListAsync());
    }

    [Fact]
    public async Task HabilitacaoNaoLiberaMesaOcupadaNemEncerraComanda()
    {
        using var b=new BancoSalao();var id=await b.Abrir();var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        await Assert.ThrowsAsync<ValidationException>(()=>b.PortariaService.AlterarHabilitacaoMesa(b.Admin.Id,1,new(){Ativa=false,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid()}));
        Assert.True((await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1)).Ativa);
        Assert.Equal(id,(await b.Consulta.Mesas(b.Portaria.Id)).Single(m=>m.Id==1).AtendimentoId);
        Assert.Equal("Ocupada",(await b.Consulta.Mesas(b.Portaria.Id)).Single(m=>m.Id==1).Situacao);
    }

    [Fact]
    public async Task HabilitacaoNaoDesfazChamadaDaPortaria()
    {
        using var b=new BancoSalao();var codigo=await b.PortariaService.Fila(b.Portaria.Id,new(){Nome="Grupo presente",Pessoas=2,ChaveSolicitacao=Guid.NewGuid()});
        await b.PortariaService.Chamar(b.Portaria.Id,1,Guid.NewGuid());var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        await Assert.ThrowsAsync<ValidationException>(()=>b.PortariaService.AlterarHabilitacaoMesa(b.Admin.Id,1,new(){Ativa=false,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid()}));
        var reserva=await b.Db.Reservas.SingleAsync(r=>r.Codigo==codigo);
        Assert.Equal(SituacaoReserva.Chamada,reserva.Situacao);Assert.Equal(1,reserva.MesaId);
        Assert.True((await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1)).Ativa);
    }

    [Theory]
    [InlineData(TipoUsuario.Cliente)]
    [InlineData(TipoUsuario.Garcom)]
    [InlineData(TipoUsuario.Portaria)]
    [InlineData(TipoUsuario.Cozinha)]
    public async Task CargoOperacionalNaoConfiguraMesa(TipoUsuario cargo)
    {
        using var b=new BancoSalao();var usuario=b.Usuarios.First(u=>u.Tipo==cargo);
        var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.PortariaService.AlterarHabilitacaoMesa(usuario.Id,1,new(){Ativa=false,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid()}));
        Assert.True(mesa.Ativa);Assert.Empty(await b.Db.MovimentosSalao.ToListAsync());
    }

    [Fact]
    public async Task RotaLegadaSemVersaoNaoContornaComandoSeguro()
    {
        using var b=new BancoSalao();
        await Assert.ThrowsAsync<RegraReservaException>(()=>b.Reservas.AlterarMesa(b.Admin.Id,1,false,default));
        Assert.True((await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1)).Ativa);
    }
}
