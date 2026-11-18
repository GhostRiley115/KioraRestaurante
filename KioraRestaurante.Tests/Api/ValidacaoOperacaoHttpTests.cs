using System.Net;
using System.Net.Http.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KioraRestaurante.Tests;

public class ValidacaoOperacaoHttpTests
{
    [Theory]
    [InlineData("rodadas", "itens")]
    [InlineData("comandas/fechar", "pagamentos")]
    public async Task ElementoNuloNaListaRecebeValidacaoSemErroInterno(string rota, string campo)
    {
        await using var b = await HostMobile.Criar();
        using var http = b.Cliente(false);
        http.DefaultRequestHeaders.Authorization = new("Bearer", b.Token(TipoUsuario.Garcom));
        var resposta = await http.PostAsJsonAsync("/api/salao/" + rota, new Dictionary<string, object?>
        {
            ["chaveSolicitacao"] = Guid.NewGuid(), [campo] = new object?[] { null }
        });

        Assert.True(resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"Esperada uma resposta de validação; recebido {(int)resposta.StatusCode}.");
        Assert.Contains("json", resposta.Content.Headers.ContentType!.MediaType);
        await using var scope = b.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.MovimentosSalao.ToListAsync());
        Assert.Empty(await db.PagamentosPresenciais.ToListAsync());
    }
}
