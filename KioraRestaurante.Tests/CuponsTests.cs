using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Banco = KioraRestaurante.Tests.CheckoutEntregaTests.Banco;

namespace KioraRestaurante.Tests;

public class CuponsTests
{
    internal static async Task<Usuario> Administrador(Banco b)
    {
        var a = new Usuario { Nome = "Admin", Email = "admin@example.com", SenhaHash = "teste", Tipo = TipoUsuario.Administrador };
        b.Db.Add(a); await b.Db.SaveChangesAsync(); return a;
    }
    internal static async Task<int> Criar(Banco b, int? limite = 10, int porCliente = 1)
    {
        var a = await Administrador(b); var agora = HorarioRestaurante.ParaLocal(b.Relogio.GetUtcNow().UtcDateTime);
        return await b.Cupons.Salvar(a.Id, new SalvarCupomDTO { Codigo = " PARCEIRO10 ", Descricao = "Promoção teste", Parceiro = "Parceiro A",
            Tipo = TipoDescontoCupom.Percentual, Valor = 10, ValorMinimoProdutos = 20, InicioLocal = agora.AddHours(-1), FimLocal = agora.AddDays(1),
            LimiteTotal = limite, LimitePorCliente = porCliente });
    }
    internal static async Task<KioraRestaurante.DTOs.Pedido.CriarPedidoRequestDTO> Aplicar(Banco b)
    {
        var dto = await b.Preparar(true);
        var resumo = (await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        var cupom = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo);
        dto.CodigoCupom = cupom.Codigo; dto.TokenCupom = cupom.Token; return dto;
    }

    [Fact] public async Task PedidoReservaUmaVezDescontaSomenteProdutosEMantemHistorico()
    {
        using var b = new Banco(); await Criar(b);
        var p = await b.Db.Produtos.SingleAsync(); p.Imagem = "https://example.org/original.webp"; p.ImagemPublicId = "produtos/original"; await b.Db.SaveChangesAsync();
        var dto = await Aplicar(b);
        var id = await b.Pedidos.ConfirmarPedido(b.Acesso, dto);
        Assert.Equal(id, await b.Pedidos.ConfirmarPedido(b.Acesso, dto));
        b.Db.ChangeTracker.Clear();
        var pedido = (await b.Pedidos.BuscarPedido(b.Cliente.Id, id))!;
        Assert.Equal(41m, pedido.ValorTotal); Assert.Equal(5, pedido.TaxaEntrega); Assert.Equal(4, pedido.DescontoCupom);
        Assert.Equal("PARCEIRO10", pedido.CodigoCupom); Assert.Equal(SituacaoUsoCupom.Reservado, (await b.Db.UsosCupom.SingleAsync()).Situacao);
        p = await b.Db.Produtos.SingleAsync(); p.Imagem = "https://example.org/nova.webp"; await b.Db.SaveChangesAsync(); b.Db.ChangeTracker.Clear();
        Assert.Equal("https://example.org/original.webp", (await b.Pedidos.BuscarPedido(b.Cliente.Id, id))!.ItensPedido.Single().ImagemUrl);
        Assert.Equal(1, (await b.Db.Cupons.SingleAsync()).UtilizacoesAtivas);
    }
    [Fact] public async Task FalhaNaCompraDesfazReservaEnderecoEventosELimpezaDoCarrinho()
    {
        using var b = new Banco(); await Criar(b); var dto = await Aplicar(b);
        await b.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_pedido BEFORE INSERT ON Pedidos BEGIN SELECT RAISE(ABORT, 'simulada'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => b.Pedidos.ConfirmarPedido(b.Acesso, dto));
        Assert.Empty(await b.Db.Pedidos.ToListAsync()); Assert.Empty(await b.Db.UsosCupom.ToListAsync()); Assert.Empty(await b.Db.EventosNotificacao.ToListAsync());
        Assert.Empty(await b.Db.EnderecosUsuario.ToListAsync()); Assert.Single(await b.Db.ItensCarrinho.ToListAsync()); Assert.Equal(0, (await b.Db.Cupons.SingleAsync()).UtilizacoesAtivas);
    }
    [Fact] public async Task CupomNaoAceitaOutroClienteCarrinhoAlteradoOuTokenAdulterado()
    {
        using var b = new Banco(); await Criar(b); var resumo = (await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        var c = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo);
        var outro = new Usuario { Nome = "Outro", Email = "outro@example.com", SenhaHash = "teste" }; b.Db.Add(outro); await b.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Conferir(outro.Id, c.Codigo, c.Token, resumo));
        await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Conferir(b.Cliente.Id, c.Codigo, "adulterado", resumo));
        resumo.Itens[0].Quantidade++;
        await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Conferir(b.Cliente.Id, c.Codigo, c.Token, resumo));
    }
    [Theory] [InlineData("pausado")] [InlineData("expirado")] [InlineData("alterado")]
    public async Task ConfirmaRegrasNovamenteAntesDaCompra(string mudanca)
    {
        using var b = new Banco(); var id = await Criar(b); var dto = await Aplicar(b); var c = await b.Db.Cupons.FindAsync(id);
        if (mudanca == "pausado") c!.Ativo = false;
        if (mudanca == "expirado") c!.FimEmUtc = b.Relogio.GetUtcNow().UtcDateTime.AddSeconds(-1);
        if (mudanca == "alterado") c!.Versao = Guid.NewGuid();
        await b.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => b.Pedidos.ConfirmarPedido(b.Acesso, dto));
        Assert.Empty(await b.Db.Pedidos.ToListAsync()); Assert.Single(await b.Db.ItensCarrinho.ToListAsync());
    }
    [Fact] public async Task CotacaoExpiraENaoPodeSerConfirmada()
    {
        using var b = new Banco(); await Criar(b); var resumo = (await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        var c = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo); b.Relogio.Agora = b.Relogio.Agora.AddMinutes(16);
        await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Conferir(b.Cliente.Id, c.Codigo, c.Token, resumo));
    }
    [Theory] [InlineData(FormaPagamento.MercadoPago, StatusPedido.Recebido, StatusPagamento.Aprovado, SituacaoUsoCupom.Confirmado)]
    [InlineData(FormaPagamento.Dinheiro, StatusPedido.Recebido, StatusPagamento.Aprovado, SituacaoUsoCupom.Reservado)]
    [InlineData(FormaPagamento.Dinheiro, StatusPedido.Entregue, StatusPagamento.Pendente, SituacaoUsoCupom.Confirmado)]
    [InlineData(FormaPagamento.MercadoPago, StatusPedido.Cancelado, StatusPagamento.Aprovado, SituacaoUsoCupom.Cancelado)]
    public async Task ContagemSeguePagamentoEntregaECancelamento(FormaPagamento forma, StatusPedido status, StatusPagamento pagamento, SituacaoUsoCupom esperado)
    {
        using var b = new Banco(); var cupomId = await Criar(b); var dto = await Aplicar(b); dto.FormaPagamento = forma;
        var id = await b.Pedidos.ConfirmarPedido(b.Acesso, dto); var pedido = await b.Db.Pedidos.SingleAsync();
        pedido.StatusPedido = status; pedido.StatusPagamento = pagamento; await b.Db.SaveChangesAsync();
        await b.Cupons.ReconciliarPedido(id); await b.Cupons.ReconciliarPedido(id);
        Assert.Equal(esperado, (await b.Db.UsosCupom.SingleAsync()).Situacao);
        Assert.Equal(esperado == SituacaoUsoCupom.Cancelado ? 0 : 1, (await b.Db.Cupons.FindAsync(cupomId))!.UtilizacoesAtivas);
        var relatorio = await b.Cupons.Relatorio((await b.Db.Usuarios.SingleAsync(u => u.Tipo == TipoUsuario.Administrador)).Id, cupomId, 1);
        Assert.Single(relatorio.Usos); Assert.Equal(esperado == SituacaoUsoCupom.Confirmado ? 4m : 0, relatorio.DescontosConfirmados);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task UltimaCotaOuLimitePorClienteSaoConferidosMesmoComCotacaoAnterior(bool global)
    {
        using var b = new Banco(); var cupomId = await Criar(b, global ? 1 : 10, global ? 10 : 1);
        var resumo = (await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        var c = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo);
        var dto = await Aplicar(b); await b.Pedidos.ConfirmarPedido(b.Acesso, dto);
        var novo = new Pedido { UsuarioId = b.Cliente.Id, CodigoCupom = c.Codigo, DescontoCupom = c.Desconto };
        await using (var tx = await b.Db.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Reservar(novo, c));
            await tx.RollbackAsync();
        }
        b.Db.ChangeTracker.Clear();
        Assert.Single(await b.Db.UsosCupom.ToListAsync()); Assert.Equal(1, (await b.Db.Cupons.FindAsync(cupomId))!.UtilizacoesAtivas);
        var anterior = await b.Db.Pedidos.SingleAsync(); anterior.StatusPedido = StatusPedido.Cancelado; await b.Db.SaveChangesAsync();
        var liberado = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo);
        Assert.Equal(4, liberado.Desconto); Assert.Equal(0, (await b.Db.Cupons.FindAsync(cupomId))!.UtilizacoesAtivas);
    }
    [Fact] public async Task DuasInstanciasDisputandoUltimaCotaConfirmamSomenteUmaReserva()
    {
        using var b = new Banco(); await Criar(b, 1, 10);
        var resumo = (await b.Pedidos.PrepararCheckout(b.Acesso)).Carrinho;
        var aplicado = await b.Cupons.Cotar(b.Cliente.Id, "parceiro10", resumo);
        var arquivo = Path.Combine(Path.GetTempPath(), $"kiora-cupom-concorrencia-{Guid.NewGuid():N}.db");
        try
        {
            using (var destino = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={arquivo}"))
            {
                destino.Open(); ((Microsoft.Data.Sqlite.SqliteConnection)b.Db.Database.GetDbConnection()).BackupDatabase(destino);
            }
            var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Reservar()
            {
                await inicio.Task;
                await using var db = new KioraRestaurante.Data.AppDbContext(new DbContextOptionsBuilder<KioraRestaurante.Data.AppDbContext>()
                    .UseSqlite($"Data Source={arquivo};Default Timeout=10;Pooling=False").Options);
                var servico = new KioraRestaurante.Services.Cupons.CupomService(db, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), TimeProvider.System);
                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var pedido = new Pedido { UsuarioId = b.Cliente.Id, CodigoCupom = aplicado.Codigo, DescontoCupom = aplicado.Desconto, ValorTotal = 41 };
                    await servico.Reservar(pedido, aplicado); db.Pedidos.Add(pedido); await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
                }
                catch (ValidationException) { await tx.RollbackAsync(); return false; }
            }
            var primeira = Task.Run(Reservar); var segunda = Task.Run(Reservar); inicio.SetResult();
            var resultados = await Task.WhenAll(primeira, segunda); Assert.Equal(1, resultados.Count(r => r));
            await using var conferir = new KioraRestaurante.Data.AppDbContext(new DbContextOptionsBuilder<KioraRestaurante.Data.AppDbContext>()
                .UseSqlite($"Data Source={arquivo};Pooling=False").Options);
            Assert.Single(await conferir.UsosCupom.ToListAsync()); Assert.Single(await conferir.Pedidos.ToListAsync());
            Assert.Equal(1, (await conferir.Cupons.SingleAsync()).UtilizacoesAtivas);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(arquivo); }
    }

    [Theory] [InlineData(TipoDescontoCupom.Percentual, 10, 33.33, 3.33)] [InlineData(TipoDescontoCupom.ValorFixo, 100, 40, 40)]
    [InlineData(TipoDescontoCupom.Percentual, 100, 40, 40)]
    public void DescontoArredondaELimitaAoSubtotal(TipoDescontoCupom tipo, decimal valor, decimal subtotal, decimal esperado)
        => Assert.Equal(esperado, KioraRestaurante.Services.Cupons.CupomService.CalcularDesconto(new Cupom { Tipo = tipo, Valor = valor }, subtotal));
    [Fact] public async Task SomenteAdministradorAtivoPodeCriarEEditarSemSobrescreverAbaAntiga()
    {
        using var b = new Banco(); var id = await Criar(b); var admin = await b.Db.Usuarios.SingleAsync(u => u.Tipo == TipoUsuario.Administrador);
        var original = await b.Cupons.BuscarEdicao(admin.Id, id); var antigo = await b.Cupons.BuscarEdicao(admin.Id, id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Cupons.Salvar(b.Cliente.Id, original));
        original.Valor = 15; await b.Cupons.Salvar(admin.Id, original);
        antigo.Valor = 90; await Assert.ThrowsAsync<ValidationException>(() => b.Cupons.Salvar(admin.Id, antigo));
        b.Db.ChangeTracker.Clear(); Assert.Equal(15, (await b.Db.Cupons.SingleAsync()).Valor);
        Assert.Single(await b.Cupons.Listar(admin.Id));
    }
}
