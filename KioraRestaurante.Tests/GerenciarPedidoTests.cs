using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Options;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Entrega;
using KioraRestaurante.Services.PagamentosOnline;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;
using Opcoes = Microsoft.Extensions.Options.Options;

namespace KioraRestaurante.Tests;

public class GerenciarPedidoTests
{
    [Fact]
    public async Task CancelamentoDePedidoRecebidoEmDinheiroEIdempotente()
    {
        using var b = new Banco(false);
        var dto = new CancelarPedidoRequestDTO { PedidoId = b.Id, VersaoPedido = (await b.Pedido()).Versao, Motivo = "  Pedi errado  " };
        var id = await b.Service.Cancelar(b.UsuarioId, dto, default);
        Assert.Equal(id, await b.Service.Cancelar(b.UsuarioId, dto, default));
        var p = await b.Pedido();
        Assert.Equal(StatusPedido.Cancelado, p.StatusPedido);
        Assert.Equal(StatusPagamento.Cancelado, p.StatusPagamento);
        Assert.Equal("Pedi errado", p.MotivoCancelamento);
        Assert.False(p.ReembolsoCancelamentoPendente);
        Assert.Single(await b.Db.OperacoesPedido.ToListAsync());
    }

    [Theory]
    [InlineData(StatusPedido.EmPreparo)]
    [InlineData(StatusPedido.SaiuParaEntrega)]
    [InlineData(StatusPedido.Entregue)]
    public async Task NaoCancelaAposIniciarPreparo(StatusPedido status)
    {
        using var b = new Banco(false);
        await b.Db.Pedidos.ExecuteUpdateAsync(s => s.SetProperty(p => p.StatusPedido, status));
        var dto = new CancelarPedidoRequestDTO { PedidoId = b.Id, VersaoPedido = (await b.Pedido()).Versao };
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.Cancelar(b.UsuarioId, dto, default));
        Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }

    [Fact]
    public async Task CancelamentoPagoMantemPagamentoRealEIndicaConferenciaDoEstorno()
    {
        using var b = new Banco(true, true);
        await b.Service.Cancelar(b.UsuarioId, new() { PedidoId = b.Id, VersaoPedido = (await b.Pedido()).Versao }, default);
        var p = await b.Pedido();
        Assert.Equal(StatusPedido.Cancelado, p.StatusPedido);
        Assert.Equal(StatusPagamento.Aprovado, p.StatusPagamento);
        Assert.True(p.ReembolsoCancelamentoPendente);
        Assert.True((await b.Db.PagamentosOnline.SingleAsync()).RevisaoNecessaria);
        Assert.Equal(0, b.Provedor.Devolucoes);
    }

    [Fact]
    public async Task AlterarEnderecoEmDinheiroRecalculaValorSemPagamentoOnline()
    {
        using var b = new Banco(false);
        var id = await b.Alterar(11000);
        var op = await b.Service.BuscarOperacao(b.UsuarioId, id, default);
        var p = await b.Pedido();
        Assert.Equal(EstadoOperacaoPedido.Concluida, op.Estado);
        Assert.Equal("99", p.EnderecoEntrega.Numero);
        Assert.Equal(12m, p.TaxaEntrega);
        Assert.Equal(52m, p.ValorTotal);
        Assert.Equal(40, p.PrazoTotalMinutos);
        Assert.Equal(0, b.Provedor.Consultas);
    }

    [Fact]
    public async Task CobrancaNaoPagaEEncerradaAntesDeAtualizarTotalERecriarCheckout()
    {
        using var b = new Banco(true, false);
        var id = await b.Alterar(7000);
        var op = await b.Service.BuscarOperacao(b.UsuarioId, id, default);
        Assert.Equal(EstadoOperacaoPedido.Concluida, op.Estado);
        Assert.NotNull(op.PagamentoAnteriorJson);
        Assert.Empty(await b.Db.PagamentosOnline.ToListAsync());
        Assert.Equal(1, b.Provedor.Cancelamentos);
        await b.Pagamentos.Iniciar(b.UsuarioId, b.Id, default);
        Assert.Equal(48m, (await b.Db.PagamentosOnline.SingleAsync()).Valor);
    }

    [Fact]
    public async Task PedidoPagoSoMudaEnderecoDepoisDePagarApenasADiferenca()
    {
        using var b = new Banco(true, true);
        var id = await b.Alterar(7000);
        var op = await b.Service.BuscarOperacao(b.UsuarioId, id, default);
        Assert.Equal(EstadoOperacaoPedido.AguardandoPagamento, op.Estado);
        Assert.Equal(3m, Assert.Single(op.Movimentos).Valor);
        Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
        b.Provedor.PagarUltima();
        await b.Service.Processar(id, default);
        var p = await b.Pedido();
        Assert.Equal("99", p.EnderecoEntrega.Numero);
        Assert.Equal(48m, p.ValorTotal);
        Assert.Equal(StatusPagamento.Aprovado, p.StatusPagamento);
        await b.Pagamentos.Sincronizar(b.Provedor.Principal.Id, default);
        Assert.Equal(StatusPagamento.Aprovado, (await b.Pedido()).StatusPagamento);
        await b.Service.Processar(id, default);
        Assert.Equal(1, b.Provedor.CriacoesAdicionais);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReduzirTaxaDePedidoPagoDevolveApenasADiferencaMesmoAposTimeout(bool timeout)
    {
        using var b = new Banco(true, true, 16m);
        b.Provedor.TimeoutDevolucao = timeout;
        var id = await b.Alterar(1000);
        if (timeout)
        {
            Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
            await b.Service.Processar(id, default);
        }
        var op = await b.Service.BuscarOperacao(b.UsuarioId, id, default);
        Assert.Equal(EstadoOperacaoPedido.Concluida, op.Estado);
        Assert.Equal(11m, Assert.Single(op.Movimentos).Valor);
        Assert.Equal(45m, (await b.Pedido()).ValorTotal);
        Assert.Equal(1, b.Provedor.Devolucoes);
        await b.Pagamentos.Sincronizar(b.Provedor.Principal.Id, default);
        Assert.Equal(StatusPagamento.Aprovado, (await b.Pedido()).StatusPagamento);
    }

    [Fact]
    public async Task AumentoPagoPodeSerDevolvidoAoRetornarAFaixaOriginal()
    {
        using var b = new Banco(true, true);
        var primeiro = await b.Alterar(7000);
        b.Provedor.PagarUltima(); await b.Service.Processar(primeiro, default);
        var segundo = await b.Alterar(1000);
        var op = await b.Service.BuscarOperacao(b.UsuarioId, segundo, default);
        Assert.Equal(EstadoOperacaoPedido.Concluida, op.Estado);
        var mov = Assert.Single(op.Movimentos);
        Assert.Equal(3m, mov.Valor);
        Assert.NotEqual(b.Provedor.Principal.Id, mov.OrderId);
        Assert.Equal(45m, (await b.Pedido()).ValorTotal);
    }

    [Fact]
    public async Task EnderecoNaoMudaSePedidoSaiuAntesDaConfirmacaoDaDiferenca()
    {
        using var b = new Banco(true, true);
        var id = await b.Alterar(7000);
        await b.Db.Pedidos.ExecuteUpdateAsync(s => s.SetProperty(p => p.StatusPedido, StatusPedido.SaiuParaEntrega));
        b.Provedor.PagarUltima(); await b.Service.Processar(id, default);
        Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
        Assert.Equal(EstadoOperacaoPedido.Conferencia, (await b.Service.BuscarOperacao(b.UsuarioId, id, default)).Estado);
    }

    [Fact]
    public async Task PodeDesistirDaCobrancaAdicionalAindaNaoPaga()
    {
        using var b = new Banco(true, true);
        var id = await b.Alterar(7000);
        await b.Service.Desistir(b.UsuarioId, id, default);
        Assert.Equal(EstadoOperacaoPedido.Desfeita, (await b.Service.BuscarOperacao(b.UsuarioId, id, default)).Estado);
        var p = await b.Pedido(); Assert.Equal("77", p.EnderecoEntrega.Numero); Assert.Null(p.OperacaoPendenteId);
    }

    [Fact]
    public async Task NaoAlteraEnderecoDeOutroClienteOuDeVersaoAntigaNemAposSaida()
    {
        using var b = new Banco(false);
        var dto = await b.Dados(1000);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => b.Service.AlterarEndereco(b.UsuarioId + 99, dto, default));
        dto.VersaoPedido = Guid.NewGuid();
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.AlterarEndereco(b.UsuarioId, dto, default));
        dto.VersaoPedido = (await b.Pedido()).Versao;
        await b.Db.Pedidos.ExecuteUpdateAsync(s => s.SetProperty(p => p.StatusPedido, StatusPedido.SaiuParaEntrega));
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.AlterarEndereco(b.UsuarioId, dto, default));
        Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
    }

    [Fact]
    public async Task CliqueRepetidoNaoCriaDuasCobrancasNemPermiteCancelarComAjustePendente()
    {
        using var b = new Banco(true, true);
        var dto = await b.Dados(7000);
        await b.Service.AlterarEndereco(b.UsuarioId, dto, default);
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.AlterarEndereco(b.UsuarioId, dto, default));
        var versao = (await b.Pedido()).Versao;
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.Cancelar(b.UsuarioId,
            new() { PedidoId = b.Id, VersaoPedido = versao }, default));
        Assert.Equal(1, b.Provedor.CriacoesAdicionais);
        Assert.Single(await b.Db.OperacoesPedido.ToListAsync());
    }

    [Fact]
    public async Task DevolucoesIguaisNaMesmaTransacaoNaoSaoConfundidasComAnteriores()
    {
        using var b = new Banco(true, true, 28m);
        await b.Alterar(26000);
        var aumento = await b.Alterar(31000);
        b.Provedor.PagarUltima(); await b.Service.Processar(aumento, default);
        var reducao = await b.Alterar(21000);
        Assert.Equal(EstadoOperacaoPedido.Concluida, (await b.Service.BuscarOperacao(b.UsuarioId, reducao, default)).Estado);
        Assert.Equal(60m, (await b.Pedido()).ValorTotal);
        Assert.Equal(3, b.Provedor.Devolucoes);
    }

    [Fact]
    public async Task DevolucaoExternaDesconhecidaExigeConferenciaENaoAlteraEndereco()
    {
        using var b = new Banco(true, true);
        b.Provedor.Principal.Devolucoes.Add(new("REFMANUAL", "PAY" + b.Provedor.Principal.Id, 1m));
        var id = await b.Alterar(7000);
        Assert.Equal(EstadoOperacaoPedido.Conferencia, (await b.Service.BuscarOperacao(b.UsuarioId, id, default)).Estado);
        Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
        Assert.Equal(0, b.Provedor.CriacoesAdicionais);
    }

    [Fact]
    public async Task FalhaAoSalvarEnderecoAposPagamentoNaoDuplicaDiferencaNaRetentativa()
    {
        using var b = new Banco(true, true);
        var id = await b.Alterar(7000);
        b.Provedor.PagarUltima();
        await b.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_endereco BEFORE UPDATE ON EnderecoEntregas BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;");
        await b.Service.Processar(id, default);
        Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
        await b.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER falhar_endereco;");
        await b.Service.Processar(id, default);
        Assert.Equal("99", (await b.Pedido()).EnderecoEntrega.Numero);
        Assert.Equal(1, b.Provedor.CriacoesAdicionais);
    }

    [Fact]
    public async Task MudancaDeEtapaDuranteConsultaExternaNaoEIgnoradaNaGravacao()
    {
        using var b = new Banco(true, true);
        var id = await b.Alterar(7000);
        b.Provedor.PagarUltima();
        b.Provedor.AoConsultar = () => b.Db.Database.ExecuteSqlRaw("UPDATE Pedidos SET StatusPedido = 2;");
        await b.Service.Processar(id, default);
        Assert.Equal(StatusPedido.SaiuParaEntrega, (await b.Pedido()).StatusPedido);
        Assert.Equal("77", (await b.Pedido()).EnderecoEntrega.Numero);
        Assert.Equal(EstadoOperacaoPedido.Conferencia, (await b.Service.BuscarOperacao(b.UsuarioId, id, default)).Estado);
    }

    [Fact]
    public async Task TotalAcimaDoLimiteENegadoAntesDeCriarSolicitacaoOuCobrarDiferenca()
    {
        using var b = new Banco(true, true);
        await b.Db.ItensPedido.ExecuteUpdateAsync(s => s.SetProperty(i => i.PrecoUnitario, 99999994m));
        var dados = await b.Dados(31000);
        await Assert.ThrowsAsync<ValidationException>(() => b.Service.AlterarEndereco(b.UsuarioId, dados, default));
        Assert.Empty(await b.Db.OperacoesPedido.ToListAsync());
        Assert.Equal(0, b.Provedor.Consultas);
    }

    [Theory] [InlineData(false, 11000, 12)] [InlineData(true, 7000, 8)] [InlineData(true, 1000, 5)]
    public async Task CorrecaoDeEnderecoMantemDescontoECobraOuDevolveApenasDiferencaDoFrete(bool online, decimal metros, decimal novaTaxa)
    {
        using var b = new Banco(online, online, 8m, 4m);
        var id = await b.Alterar(metros);
        var op = await b.Service.BuscarOperacao(b.UsuarioId, id, default);
        if (op.Estado == EstadoOperacaoPedido.AguardandoPagamento) { b.Provedor.PagarUltima(); await b.Service.Processar(id, default); }
        var p = await b.Pedido(); Assert.Equal(4m, p.DescontoCupom); Assert.Equal("CUPOM", p.CodigoCupom);
        Assert.Equal(40m - 4m + novaTaxa, p.ValorTotal); Assert.Equal("99", p.EnderecoEntrega.Numero);
        if (online) { await b.Pagamentos.Sincronizar(b.Provedor.Principal.Id, default); Assert.Equal(StatusPagamento.Aprovado, (await b.Pedido()).StatusPagamento); }
    }

    private sealed class Banco : IDisposable
    {
        private readonly SqliteConnection _conn = new("Data Source=:memory:");
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly HttpClient _cepHttp = new(new CepFalso()) { BaseAddress = new("https://viacep.com.br/ws/") };
        private readonly HttpClient _mpHttp;
        private readonly RotasEntregaSimuladas _rotas = new();
        public AppDbContext Db { get; }
        public GerenciarPedidoService Service { get; }
        public CheckoutOnlineService Pagamentos { get; }
        public Provedor Provedor { get; } = new();
        public int Id { get; }
        public int UsuarioId { get; }

        public Banco(bool online, bool pago = false, decimal taxa = 5m, decimal cupom = 0)
        {
            _conn.Open(); Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options); Db.Database.EnsureCreated();
            var usuario = new Usuario { Nome = "Cliente", Email = "cliente@example.com", SenhaHash = "sem-uso" };
            var p = new Pedido { Usuario = usuario, FormaPagamento = online ? FormaPagamento.MercadoPago : FormaPagamento.Dinheiro,
                StatusPagamento = pago ? StatusPagamento.Aprovado : StatusPagamento.Pendente, TaxaEntrega = taxa, ValorTotal = 40m - cupom + taxa, DescontoCupom = cupom, CodigoCupom = cupom > 0 ? "CUPOM" : null,
                EnderecoEntrega = new() { Cep = "05422000", Logradouro = "Rua de Teste", Numero = "77", Bairro = "Pinheiros", Cidade = "São Paulo", Uf = "SP" },
                ItensPedido = [new() { Produto = new Produto { Nome = "Prato", Descricao = "Teste", CategoriaId = 2, Preco = 40m }, NomeProduto = "Prato", Quantidade = 1, PrecoUnitario = 40m }], PreparoMinutos = 30, DuracaoPercursoSegundos = 600 };
            Db.Add(p); Db.SaveChanges(); Id = p.Id; UsuarioId = usuario.Id;
            if (online)
            {
                var registro = new PagamentoOnline { PedidoId = p.Id, Valor = p.ValorTotal };
                registro.CorpoCriacao = JsonSerializer.Serialize(new { external_reference = registro.Referencia.ToString("N"), total_amount = p.ValorTotal.ToString("0.00", CultureInfo.InvariantCulture) });
                Provedor.Inicializar(registro.Referencia, registro.Valor, pago); registro.OrderId = Provedor.Principal.Id;
                Db.Add(registro); Db.SaveChanges();
            }
            var config = Opcoes.Create(new MercadoPagoConfig { Ativado = true, ModoTeste = true, AccessToken = "ficticio", ApplicationId = "456", VendedorId = "123", UrlPublica = "https://kiora.example.com" });
            _mpHttp = new(Provedor) { BaseAddress = new("https://api.mercadopago.com/") };
            var api = new MpOrdersClient(_mpHttp, config); Pagamentos = new(Db, api, config);
            var enderecos = new EnderecoUsuarioService(Db, new CepService(_cepHttp, _cache, new ConfigurationBuilder().Build()));
            var entrega = new CotacaoEntregaService(_rotas, Opcoes.Create(new EntregaOptions()), new EphemeralDataProtectionProvider(), TimeProvider.System);
            Service = new(Db, enderecos, entrega, api, Pagamentos, config, TimeProvider.System, new LoggerTeste());
            Db.ChangeTracker.Clear();
        }
        public Task<Pedido> Pedido() => Db.Pedidos.AsNoTracking().Include(p => p.EnderecoEntrega).SingleAsync();
        public async Task<AlterarEnderecoPedidoRequestDTO> Dados(decimal metros)
        {
            _rotas.Metros = metros;
            var p = await Pedido();
            var dto = new AlterarEnderecoPedidoRequestDTO { PedidoId = p.Id, VersaoPedido = p.Versao,
                NovoEndereco = new() { Cep = "05422000", Logradouro = "Rua de Teste", Numero = "99", Bairro = "Pinheiros" } };
            dto.TokenEntrega = (await Service.Cotar(UsuarioId, Id, p.Versao, dto.SomenteEndereco(), default)).Token;
            return dto;
        }
        public async Task<Guid> Alterar(decimal metros) => await Service.AlterarEndereco(UsuarioId, await Dados(metros), default);
        public void Dispose() { _mpHttp.Dispose(); _cepHttp.Dispose(); Db.Dispose(); _conn.Dispose(); _cache.Dispose(); }
    }

    private sealed class CepFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("""{"logradouro":"Rua de Teste","bairro":"Pinheiros","localidade":"São Paulo","uf":"SP","ibge":"3550308"}""") });
    }

    private sealed class LoggerTeste : ILogger<GerenciarPedidoService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Console.WriteLine(formatter(state, exception));
    }

    private sealed class Provedor : HttpMessageHandler
    {
        private readonly Dictionary<string, Ordem> _ordens = [];
        private readonly Dictionary<string, Ordem> _criadas = [];
        private readonly Dictionary<string, Devolucao> _devolvidas = [];
        public Ordem Principal { get; private set; } = null!;
        public int Consultas { get; private set; }
        public int Cancelamentos { get; private set; }
        public int Devolucoes => _devolvidas.Count;
        public int CriacoesAdicionais => _ordens.Count - 1;
        public bool TimeoutDevolucao { get; set; }
        public Action? AoConsultar { get; set; }
        public void Inicializar(Guid referencia, decimal valor, bool pago)
        { Principal = new("ORDTST1", referencia.ToString("N"), valor) { Pago = pago }; _ordens.Add(Principal.Id, Principal); }
        public void PagarUltima() => _ordens.Values.Last().Pago = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Consultas++;
            var partes = req.RequestUri!.AbsolutePath.Trim('/').Split('/');
            var chave = req.Headers.TryGetValues("X-Idempotency-Key", out var keys) ? keys.Single() : "";
            Ordem ordem;
            if (partes.Length == 2 && req.Method == HttpMethod.Post)
            {
                if (!_criadas.TryGetValue(chave, out ordem!))
                {
                    using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct));
                    ordem = new("ORDTST" + (_ordens.Count + 1), body.RootElement.GetProperty("external_reference").GetString()!, decimal.Parse(body.RootElement.GetProperty("total_amount").GetString()!, CultureInfo.InvariantCulture));
                    _ordens.Add(ordem.Id, ordem); _criadas.Add(chave, ordem);
                }
            }
            else ordem = _ordens[partes[2]];
            if (req.Method == HttpMethod.Get) AoConsultar?.Invoke();
            if (partes.Last() == "cancel")
            {
                if (ordem.Pago) return new(HttpStatusCode.BadRequest) { Content = new StringContent("{}") };
                if (!ordem.Cancelada) Cancelamentos++;
                ordem.Cancelada = true;
            }
            if (partes.Last() == "refund")
            {
                if (!_devolvidas.ContainsKey(chave))
                {
                    using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct));
                    var t = body.RootElement.GetProperty("transactions")[0];
                    var d = new Devolucao("REF" + (_devolvidas.Count + 1), t.GetProperty("id").GetString()!, decimal.Parse(t.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture));
                    _devolvidas.Add(chave, d); ordem.Devolucoes.Add(d);
                    if (TimeoutDevolucao) { TimeoutDevolucao = false; throw new HttpRequestException("Timeout após processar, simulado"); }
                }
            }
            var devolvido = ordem.Devolucoes.Sum(d => d.Valor);
            var json = JsonSerializer.Serialize(new
            {
                id = ordem.Id, type = "online", processing_mode = "manual", external_reference = ordem.Referencia,
                user_id = "123", currency = "BRL", country_code = "BRA", total_amount = ordem.Valor.ToString("0.00", CultureInfo.InvariantCulture),
                integration_data = new { application_id = "456" }, checkout_url = "https://www.mercadopago.com.br/checkout/v1/redirect?order_id=" + ordem.Id,
                status = ordem.Cancelada ? "canceled" : devolvido == ordem.Valor ? "refunded" : ordem.Pago ? "processed" : "created",
                status_detail = ordem.Cancelada ? "canceled" : devolvido == ordem.Valor ? "refunded" : devolvido > 0 ? "partially_refunded" : ordem.Pago ? "accredited" : "created",
                total_paid_amount = ordem.Pago ? ordem.Valor.ToString("0.00", CultureInfo.InvariantCulture) : null,
                last_updated_date = DateTimeOffset.UtcNow.ToString("O"),
                transactions = new { payments = new[] { new { id = "PAY" + ordem.Id, amount = ordem.Valor.ToString("0.00", CultureInfo.InvariantCulture) } },
                    refunds = ordem.Devolucoes.Select(d => new { id = d.Id, transaction_id = d.TransacaoId, amount = d.Valor.ToString("0.00", CultureInfo.InvariantCulture), status = "processed" }).ToArray() }
            });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
        public sealed class Ordem(string id, string referencia, decimal valor)
        { public string Id { get; } = id; public string Referencia { get; } = referencia; public decimal Valor { get; } = valor; public bool Pago { get; set; } public bool Cancelada { get; set; } public List<Devolucao> Devolucoes { get; } = []; }
        public sealed record Devolucao(string Id, string TransacaoId, decimal Valor);
    }
}
