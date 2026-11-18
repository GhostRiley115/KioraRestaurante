using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using KioraRestaurante.Services.Usuarios;

namespace KioraRestaurante.Tests;

public class DesativacaoConcorrenciaTests
{
    [Fact]
    public async Task DoisAdministradoresNaoConseguemDesativarAmbosSimultaneamente()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"kiora-admins-{Guid.NewGuid():N}.db");
        try
        {
            var primeiro = new Usuario { Nome = "Primeiro", Email = "primeiro@example.com", Tipo = TipoUsuario.Administrador, SenhaHash = "teste" };
            var segundo = new Usuario { Nome = "Segundo", Email = "segundo@example.com", Tipo = TipoUsuario.Administrador, SenhaHash = "teste" };
            using (var db = Banco(arquivo))
            {
                db.Database.EnsureCreated();
                db.AddRange(primeiro, segundo);
                db.SaveChanges();
            }
            var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Desativar(Usuario usuario)
            {
                await inicio.Task;
                using var db = Banco(arquivo);
                try { return new UsuarioService(db).DesativarConta(usuario.Id, usuario.VersaoSessao); }
                catch (ValidationException) { return false; }
            }
            var a = Task.Run(() => Desativar(primeiro));
            var b = Task.Run(() => Desativar(segundo));
            inicio.SetResult();
            Assert.Equal(1, (await Task.WhenAll(a, b)).Count(sucesso => sucesso));
            using var conferir = Banco(arquivo);
            Assert.Equal(1, await conferir.Usuarios.CountAsync(u => u.Ativo && u.Tipo == TipoUsuario.Administrador));
        }
        finally { File.Delete(arquivo); }
    }

    [Fact]
    public async Task AutodesativacaoExigeTransferirMesaERevogaAsVersoesAoConcluir()
    {
        using var b = new BancoSalao();
        var id = await b.Abrir();
        var sessao = b.Garcom.VersaoSessao;
        var cadastro = b.Garcom.VersaoFuncionario;
        var servico = new UsuarioService(b.Db);
        Assert.Throws<ValidationException>(() => servico.DesativarConta(b.Garcom.Id, sessao));
        Assert.True((await b.Db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == b.Garcom.Id)).Ativo);
        var comanda = await b.Detalhe(id);
        await b.Comandas.Transferir(b.Gerente.Id, new()
        {
            AtendimentoId = id, Versao = comanda.Versao, GarcomId = b.OutroGarcom.Id, ChaveSolicitacao = Guid.NewGuid()
        });
        Assert.True(servico.DesativarConta(b.Garcom.Id, sessao));
        var atual = await b.Db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == b.Garcom.Id);
        Assert.False(atual.Ativo);
        Assert.NotEqual(sessao, atual.VersaoSessao);
        Assert.NotEqual(cadastro, atual.VersaoFuncionario);
        Assert.Equal(b.OutroGarcom.Id, (await b.Db.AtendimentosMesa.AsNoTracking().SingleAsync()).GarcomId);
    }

    [Fact]
    public void LogoutRevogaApenasAVersaoConferidaENaoEncerraOutraSessaoPosterior()
    {
        using var b = new BancoSalao();
        var servico = new UsuarioService(b.Db);
        var anterior = b.Cliente.VersaoSessao;
        Assert.True(servico.RevogarSessoes(b.Cliente.Id, anterior));
        var atual = b.Db.Usuarios.AsNoTracking().Single(u => u.Id == b.Cliente.Id);
        Assert.NotEqual(anterior, atual.VersaoSessao);
        Assert.False(servico.RevogarSessoes(b.Cliente.Id, anterior));
        Assert.False(servico.DesativarConta(b.Cliente.Id, anterior));
        var preservado = b.Db.Usuarios.AsNoTracking().Single(u => u.Id == b.Cliente.Id);
        Assert.True(preservado.Ativo);
        Assert.Equal(atual.VersaoSessao, preservado.VersaoSessao);
    }

    [Fact]
    public void UltimoAdministradorContinuaAtivo()
    {
        using var b = new BancoSalao();
        Assert.Throws<ValidationException>(() => new UsuarioService(b.Db).DesativarConta(b.Admin.Id, b.Admin.VersaoSessao));
        Assert.True(b.Db.Usuarios.AsNoTracking().Single(u => u.Id == b.Admin.Id).Ativo);
    }

    private static AppDbContext Banco(string arquivo) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={arquivo};Default Timeout=10;Pooling=False").Options);
}
