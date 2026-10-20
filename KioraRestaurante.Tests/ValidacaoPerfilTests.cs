using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KioraRestaurante.Tests;

public class ValidacaoPerfilTests
{
    [Theory]
    [InlineData(100, 150, true)]
    [InlineData(101, 150, false)]
    [InlineData(100, 151, false)]
    public void FormulariosRespeitamLimitesDoBanco(int tamanhoNome, int tamanhoEmail, bool valido)
    {
        var nome = new string('N', tamanhoNome);
        var email = new string('a', tamanhoEmail - "@example.test".Length) + "@example.test";
        object[] formularios =
        [
            new CadastroUsuarioViewModel
            {
                Nome = nome, Email = email, Senha = "Teste123!",
                ConfirmarSenha = "Teste123!", AceiteTermos = true
            },
            new EditarPerfilViewModel { Nome = nome, Email = email }
        ];
        foreach (var formulario in formularios)
        {
            var erros = new List<ValidationResult>();
            Assert.Equal(valido, Validator.TryValidateObject(formulario,
                new ValidationContext(formulario), erros, validateAllProperties: true));
            if (!valido)
                Assert.Contains(erros, erro => erro.ErrorMessage!.Contains("no máximo"));
        }
    }

    [Theory]
    [InlineData(101, 20)]
    [InlineData(10, 151)]
    public void ServiceRecusaDadosLongosAntesDeConsultarOuGravarBanco(int tamanhoNome, int tamanhoEmail)
    {
        // Sem provedor de banco: qualquer consulta indevida faria o teste falhar.
        using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var service = new UsuarioService(db);
        var nome = new string('N', tamanhoNome);
        var email = new string('a', tamanhoEmail - "@example.test".Length) + "@example.test";
        Assert.Throws<ValidationException>(() => service.Cadastrar(new UsuarioCadastroDTO
        {
            Nome = nome, Email = email, Senha = "Teste123!",
            ConfirmarSenha = "Teste123!", AceiteTermos = true
        }));
        Assert.Throws<ValidationException>(() => service.AtualizarPerfil(1,
            new UsuarioAtualizarPerfilDTO { Nome = nome, Email = email }));
    }
}
