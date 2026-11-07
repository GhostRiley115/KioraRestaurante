using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KioraRestaurante.Tests;
public class SalaoTests
{
    [Fact] public async Task FluxoCompletoMantemRodadasFechamentoELimpezaSeparados()
    {
        using var b=new BancoSalao();var id=await b.Abrir();
        await b.Adicionar(id,2,"Sem cebola");
        var itens=await b.Cozinha.Fila(b.Cozinheiro.Id);Assert.Single(itens);Assert.Equal("salao",itens[0].Origem);
        await b.PrepararItem(id);await b.Entregar(id);
        await b.Adicionar(id,1,"Pouco molho");await b.PrepararItem(id);await b.Entregar(id);
        var a=await b.Detalhe(id);Assert.Equal(60,a.Total);Assert.Equal(2,a.Itens.Count);
        await b.Comandas.Descontar(b.Gerente.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Tipo=TipoDescontoPresencial.Percentual,Valor=10,Motivo="Parceiro"});
        a=await b.Detalhe(id);Assert.Equal(54,a.Total);Assert.Equal(6,a.Desconto);
        var fechar=new FecharComandaDTO{AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),PagamentoExternoConferido=true,
            Pagamentos=[new(){Meio=MeioPagamentoPresencial.Pix,Valor=30},new(){Meio=MeioPagamentoPresencial.CartaoCredito,Valor=24}]};
        await b.Comandas.Fechar(b.Garcom.Id,fechar);await b.Comandas.Fechar(b.Garcom.Id,fechar);
        a=await b.Detalhe(id);Assert.Equal(SituacaoAtendimento.AguardandoLimpeza,a.Situacao);Assert.Equal(2,a.Pagamentos.Count);
        Assert.Equal("Aguardando limpeza",(await b.Consulta.Mesas(b.Portaria.Id)).Single(m=>m.Id==1).Situacao);
        await b.Comandas.Liberar(b.Garcom.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid()});
        Assert.Equal("Livre",(await b.Consulta.Mesas(b.Portaria.Id)).Single(m=>m.Id==1).Situacao);
        var rel=await b.Consulta.Relatorio(b.Gerente.Id,b.Horarios.Hoje,b.Horarios.Hoje.AddDays(-1));
        Assert.Equal(54,rel.Atual.Vendas);Assert.Equal(1,rel.Atual.ContasFechadas);Assert.Equal(0,rel.Comparado.Vendas);
        Assert.Empty(await b.Db.PagamentosOnline.ToListAsync());Assert.Empty(await b.Db.Pedidos.ToListAsync());
    }
    [Fact] public async Task ReenvioNaoDuplicaRodadaEChaveNaoPodeSerUsadaComOutroConteudo()
    {
        using var b=new BancoSalao();var id=await b.Abrir();var a=await b.Detalhe(id);
        var dto=new AdicionarRodadaDTO{AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Itens=[new(){ProdutoId=b.Produto.Id,Quantidade=2}]};
        await b.Comandas.Adicionar(b.Garcom.Id,dto);await b.Comandas.Adicionar(b.Garcom.Id,dto);
        Assert.Single(await b.Db.ItensComanda.ToListAsync());dto.Itens[0].Quantidade=3;
        await Assert.ThrowsAsync<ValidationException>(()=>b.Comandas.Adicionar(b.Garcom.Id,dto));
        Assert.Equal(2,(await b.Db.ItensComanda.SingleAsync()).Quantidade);
    }
    [Fact] public async Task MesaOcupadaNaoPodeSerAbertaOutraVezNemDesativada()
    {
        using var b=new BancoSalao();await b.Abrir();var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        await Assert.ThrowsAsync<ValidationException>(()=>b.Comandas.Acomodar(b.Garcom.Id,new(){MesaId=1,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid(),Pessoas=2}));
        await Assert.ThrowsAsync<ValidationException>(()=>b.PortariaService.ConfigurarMesa(b.Gerente.Id,new(){MesaId=1,Nome=mesa.Nome,Capacidade=2,Ativa=false,Versao=mesa.Versao,ChaveSolicitacao=Guid.NewGuid()}));
        Assert.Single(await b.Db.AtendimentosMesa.ToListAsync());
    }
    [Fact] public async Task GarcomOperaSomenteSuasMesasEGerentePodeTransferir()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Consulta.Comanda(b.OutroGarcom.Id,id));
        var a=await b.Detalhe(id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Comandas.Transferir(b.Garcom.Id,new(){AtendimentoId=id,GarcomId=b.OutroGarcom.Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid()}));
        await b.Comandas.Transferir(b.Gerente.Id,new(){AtendimentoId=id,GarcomId=b.OutroGarcom.Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid()});
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Consulta.Comanda(b.Garcom.Id,id));
        Assert.Equal(b.OutroGarcom.Id,(await b.Consulta.Comanda(b.OutroGarcom.Id,id)).GarcomId);
    }
    [Fact] public async Task CorrecaoPreservaHistoricoEAposPreparoSomenteGerente()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,2);
        var item=(await b.Cozinha.Fila(b.Cozinheiro.Id)).Single();await b.Cozinha.Atualizar(b.Cozinheiro.Id,new(){Origem="salao",ItemSalaoId=item.ItemSalaoId,Versao=item.Versao,Acao="iniciar",ChaveSolicitacao=Guid.NewGuid()});
        var a=await b.Detalhe(id);var dto=new CorrigirItemDTO{AtendimentoId=id,ItemId=a.Itens[0].Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Quantidade=1,Motivo="Quantidade incorreta"};
        await Assert.ThrowsAsync<ValidationException>(()=>b.Comandas.Corrigir(b.Garcom.Id,dto));
        await b.Comandas.Corrigir(b.Gerente.Id,dto);a=await b.Detalhe(id);
        Assert.Equal(2,a.Itens.Count);Assert.Equal(20,a.Total);Assert.Single(a.Itens,i=>i.Situacao==SituacaoPreparo.Cancelado);
        Assert.Single(await b.Db.Notificacoes.Where(n=>n.Tipo==KioraRestaurante.Models.Notificacoes.TipoNotificacao.CorrecaoCozinha).ToListAsync());
        Assert.Single(await b.Cozinha.Fila(b.Cozinheiro.Id));
    }
    [Fact] public async Task AbaAntigaNaoSobrescreveCorrecaoDaCozinha()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);var a=await b.Detalhe(id);
        await b.PrepararItem(id);
        await Assert.ThrowsAsync<ValidationException>(()=>b.Comandas.Corrigir(b.Gerente.Id,new(){AtendimentoId=id,ItemId=a.Itens[0].Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Quantidade=0,Motivo="Antiga"}));
        Assert.Equal(SituacaoPreparo.Pronto,(await b.Detalhe(id)).Itens[0].Situacao);
    }
    [Theory] [InlineData("pendente")] [InlineData("valor")] [InlineData("conferencia")]
    public async Task FechamentoBloqueiaItensPendentesOuRecebimentoDivergente(string caso)
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);
        if(caso!="pendente"){await b.PrepararItem(id);await b.Entregar(id);}
        var a=await b.Detalhe(id);
        await Assert.ThrowsAsync<ValidationException>(()=>b.Comandas.Fechar(b.Garcom.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),PagamentoExternoConferido=caso!="conferencia",Pagamentos=[new(){Valor=caso=="valor"?19:20}]}));
        Assert.Equal(SituacaoAtendimento.Aberto,(await b.Detalhe(id)).Situacao);Assert.Empty(await b.Db.PagamentosPresenciais.ToListAsync());
    }
    [Fact] public async Task CortesiaIntegralNaoCriaPagamentoFalsoENaoApagaConsumo()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);await b.PrepararItem(id);await b.Entregar(id);
        var a=await b.Detalhe(id);await b.Comandas.Descontar(b.Admin.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Tipo=TipoDescontoPresencial.Percentual,Valor=100,Motivo="Cortesia autorizada"});
        a=await b.Detalhe(id);await b.Comandas.Fechar(b.Garcom.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),PagamentoExternoConferido=true});
        a=await b.Detalhe(id);Assert.Equal(20,a.Subtotal);Assert.Equal(0,a.Total);Assert.Empty(a.Pagamentos);Assert.Single(a.Itens);
    }
    [Fact] public async Task FalhaAoSalvarRodadaNaoDeixaItensNemAuditoriaParcial()
    {
        using var b=new BancoSalao();var id=await b.Abrir();var antes=await b.Db.MovimentosSalao.CountAsync();
        await b.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER falhar_item BEFORE INSERT ON ItensComanda BEGIN SELECT RAISE(ABORT,'simulada'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(()=>b.Adicionar(id,1));
        Assert.Empty(await b.Db.ItensComanda.ToListAsync());Assert.Equal(antes,await b.Db.MovimentosSalao.CountAsync());
    }
    [Fact] public async Task ReservaAguardaChegadaPortariaNaoAtivaMesaEGarcomAcomodaSemPermanencia()
    {
        using var b=new BancoSalao();var dto=new CriarReservaDTO{ChaveSolicitacao=Guid.NewGuid(),Nome="Grupo",Telefone="11900000000",Pessoas=2,Data=b.Horarios.Hoje,Horario="12:00"};
        var reserva=await b.Reservas.Criar(new(b.Cliente.Id,null),dto,false,default);
        Assert.Equal(SituacaoReserva.AguardandoChegada,reserva.Situacao);Assert.Null(reserva.FimUtc);Assert.Empty(await b.Db.AtendimentosMesa.ToListAsync());
        await b.PortariaService.Chegada(b.Portaria.Id,new(){Codigo=reserva.Codigo,Versao=reserva.Versao,ChaveSolicitacao=Guid.NewGuid()});
        Assert.Empty(await b.Db.AtendimentosMesa.ToListAsync());
        await b.PortariaService.Chamar(b.Portaria.Id,1,Guid.NewGuid());var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        var id=await b.Comandas.Acomodar(b.Garcom.Id,new(){MesaId=1,Versao=mesa.Versao,ReservaCodigo=reserva.Codigo,ChaveSolicitacao=Guid.NewGuid()});
        b.Relogio.Agora=b.Relogio.Agora.AddHours(10);await b.Reservas.Manutencao(default);
        Assert.Equal(SituacaoAtendimento.Aberto,(await b.Detalhe(id)).Situacao);Assert.Equal(SituacaoReserva.EmAtendimento,(await b.Db.Reservas.SingleAsync()).Situacao);
    }
    [Fact] public async Task ChamadaExpiradaVoltaAFilaMasAtendimentoNuncaExpira()
    {
        using var b=new BancoSalao();var codigo=await b.PortariaService.Fila(b.Portaria.Id,new(){Nome="Grupo",Pessoas=2,ChaveSolicitacao=Guid.NewGuid()});
        await b.PortariaService.Chamar(b.Portaria.Id,1,Guid.NewGuid());b.Relogio.Agora=b.Relogio.Agora.AddMinutes(11);await b.Reservas.Manutencao(default);
        var r=await b.Db.Reservas.SingleAsync();Assert.Equal(SituacaoReserva.Aguardando,r.Situacao);Assert.Null(r.MesaId);
    }
    [Fact] public async Task CozinhaDeliverySoLiberaPedidoPagoOuDinheiroENaoMarcaEntrega()
    {
        using var b=new BancoSalao();var pago=b.Delivery(true);var pendente=b.Delivery(false);b.Db.AddRange(pago,pendente);await b.Db.SaveChangesAsync();
        var item=Assert.Single(await b.Cozinha.Fila(b.Cozinheiro.Id));Assert.Equal("delivery",item.Origem);
        await b.Cozinha.Atualizar(b.Cozinheiro.Id,new(){Origem="delivery",ItemDeliveryId=item.ItemDeliveryId,Versao=item.Versao,Acao="iniciar",ChaveSolicitacao=Guid.NewGuid()});
        item=Assert.Single(await b.Cozinha.Fila(b.Cozinheiro.Id));
        await b.Cozinha.Atualizar(b.Cozinheiro.Id,new(){Origem="delivery",ItemDeliveryId=item.ItemDeliveryId,Versao=item.Versao,Acao="pronto",ChaveSolicitacao=Guid.NewGuid()});
        Assert.Equal(StatusPedido.EmPreparo,(await b.Db.Pedidos.SingleAsync(p=>p.Id==pago.Id)).StatusPedido);
        Assert.Equal(StatusPedido.Recebido,(await b.Db.Pedidos.SingleAsync(p=>p.Id==pendente.Id)).StatusPedido);
    }
    [Theory] [InlineData(TipoUsuario.Cliente)] [InlineData(TipoUsuario.Portaria)] [InlineData(TipoUsuario.Cozinha)]
    public async Task CargosSemPermissaoNaoLancamItensNemVeemComanda(TipoUsuario cargo)
    {
        using var b=new BancoSalao();var id=await b.Abrir();var u=b.Usuarios.Single(u=>u.Tipo==cargo);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Consulta.Comanda(u.Id,id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>b.Comandas.Adicionar(u.Id,new(){AtendimentoId=id,ChaveSolicitacao=Guid.NewGuid(),Itens=[new(){ProdutoId=b.Produto.Id,Quantidade=1}]}));
    }
    [Fact] public async Task HistoricoPreservaNomePrecoEFotoEApenasVendasFisicasEntramNoRelatorio()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);await b.PrepararItem(id);await b.Entregar(id);
        var a=await b.Detalhe(id);await b.Comandas.Fechar(b.Garcom.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),PagamentoExternoConferido=true,Pagamentos=[new(){Valor=20}]});
        b.Produto.Nome="Novo nome";b.Produto.Preco=999;b.Produto.Imagem="nova";b.Db.Add(b.Delivery(true));await b.Db.SaveChangesAsync();
        a=await b.Detalhe(id);Assert.Equal("Lámen",a.Itens[0].Nome);Assert.Equal(20,a.Itens[0].PrecoUnitario);Assert.Equal("/imagens/produto-sem-foto.svg",a.Itens[0].ImagemUrl);
        Assert.Equal(20,(await b.Consulta.Relatorio(b.Admin.Id,b.Horarios.Hoje,b.Horarios.Hoje.AddDays(-1))).Atual.Vendas);
    }
}

internal sealed class BancoSalao:IDisposable
{
    private readonly SqliteConnection _conn=new("Data Source=:memory:");
    private readonly HttpClient _http;
    public AppDbContext Db{get;}
    public CheckoutEntregaTests.RelogioFalso Relogio{get;}=new(){Agora=new DateTimeOffset(2026,11,7,15,0,0,TimeSpan.Zero)};
    public Usuario Cliente{get;}=new(){Nome="Cliente",Email="cliente@example.com",SenhaHash="teste"};
    public Usuario Admin{get;}=new(){Nome="Admin",Email="admin@example.com",SenhaHash="teste",Tipo=TipoUsuario.Administrador};
    public Usuario Gerente{get;}=new(){Nome="Gerente",Email="gerente@example.com",SenhaHash="teste",Tipo=TipoUsuario.Gerente};
    public Usuario Garcom{get;}=new(){Nome="Garçom",Email="garcom@example.com",SenhaHash="teste",Tipo=TipoUsuario.Garcom};
    public Usuario OutroGarcom{get;}=new(){Nome="Outro Garçom",Email="outro@example.com",SenhaHash="teste",Tipo=TipoUsuario.Garcom};
    public Usuario Portaria{get;}=new(){Nome="Portaria",Email="portaria@example.com",SenhaHash="teste",Tipo=TipoUsuario.Portaria};
    public Usuario Cozinheiro{get;}=new(){Nome="Cozinha",Email="cozinha@example.com",SenhaHash="teste",Tipo=TipoUsuario.Cozinha};
    public Usuario[] Usuarios=>[Cliente,Admin,Gerente,Garcom,OutroGarcom,Portaria,Cozinheiro];
    public Produto Produto{get;}=new(){Nome="Lámen",Descricao="Teste",CategoriaId=2,Preco=20,Imagem="/imagens/produto-sem-foto.svg"};
    public AcessoSalaoService Acesso{get;}
    public ComandoSalaoService Comandos{get;}
    public AvisosSalaoService Avisos{get;}
    public ComandaService Comandas{get;}
    public ConsultaSalaoService Consulta{get;}
    public PortariaService PortariaService{get;}
    public CozinhaService Cozinha{get;}
    public HorariosReserva Horarios{get;}
    public ReservaService Reservas{get;}
    public FuncionarioService Funcionarios{get;}
    public EmailsFalsos Emails{get;}=new();
    public BancoSalao()
    {
        _conn.Open();Db=new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);Db.Database.EnsureCreated();Db.AddRange(Usuarios);Db.Add(Produto);Db.SaveChanges();
        Acesso=new(Db);Comandos=new(Db,Relogio,Acesso);Horarios=new(Microsoft.Extensions.Options.Options.Create(new KioraRestaurante.Options.ReservaOptions()),Relogio);
        Avisos=new(Db,Relogio);Comandas=new(Db,Acesso,Comandos,Horarios,Relogio,Avisos);Consulta=new(Db,Acesso,Relogio);PortariaService=new(Db,Acesso,Comandos,Horarios);Cozinha=new(Db,Acesso,Comandos,Avisos,Relogio);Reservas=new(Db,Horarios);
        _http=new(Emails);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Brevo:ApiKey"]="teste-ficticio",["Brevo:RemetenteEmail"]="equipe@example.com",["Aplicacao:UrlPublica"]="https://kiora.example.com"}).Build();
        Funcionarios=new(Db,Acesso,new EmailService(_http,config,NullLogger<EmailService>.Instance),config,Relogio);
    }
    public async Task<Guid> Abrir(int mesaId=1)
    {var mesa=await Db.MesasRestaurante.SingleAsync(m=>m.Id==mesaId);return await Comandas.Acomodar(Garcom.Id,new(){MesaId=mesaId,Versao=mesa.Versao,Pessoas=2,ChaveSolicitacao=Guid.NewGuid()});}
    public Task<ComandaDetalhesDTO> Detalhe(Guid id)=>Consulta.Comanda(Gerente.Id,id);
    public async Task Adicionar(Guid id,int quantidade,string? observacao=null)
    {var a=await Detalhe(id);await Comandas.Adicionar(Garcom.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Itens=[new(){ProdutoId=Produto.Id,Quantidade=quantidade,Observacao=observacao}]});}
    public async Task PrepararItem(Guid id)
    {
        var item=(await Cozinha.Fila(Cozinheiro.Id)).First(i=>i.AtendimentoId==id&&i.Situacao==SituacaoPreparo.NaFila);
        await Cozinha.Atualizar(Cozinheiro.Id,new(){Origem="salao",ItemSalaoId=item.ItemSalaoId,Versao=item.Versao,Acao="iniciar",ChaveSolicitacao=Guid.NewGuid()});
        item=(await Cozinha.Fila(Cozinheiro.Id)).First(i=>i.ItemSalaoId==item.ItemSalaoId);
        await Cozinha.Atualizar(Cozinheiro.Id,new(){Origem="salao",ItemSalaoId=item.ItemSalaoId,Versao=item.Versao,Acao="pronto",ChaveSolicitacao=Guid.NewGuid()});
    }
    public async Task Entregar(Guid id)
    {var a=await Detalhe(id);await Comandas.Entregar(Garcom.Id,new(){AtendimentoId=id,ItemId=a.Itens.First(i=>i.Situacao==SituacaoPreparo.Pronto).Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid()});}
    public Pedido Delivery(bool pago)=>new(){UsuarioId=Cliente.Id,FormaPagamento=FormaPagamento.MercadoPago,StatusPagamento=pago?StatusPagamento.Aprovado:StatusPagamento.Pendente,ValorTotal=20,ItensPedido=[new(){ProdutoId=Produto.Id,Quantidade=1,PrecoUnitario=20,NomeProduto=Produto.Nome}],DataPedido=Relogio.GetUtcNow().UtcDateTime};
    public void Dispose(){Db.Dispose();_conn.Dispose();_http.Dispose();}
}
internal sealed class EmailsFalsos:HttpMessageHandler
{
    public List<string> Corpos{get;}=[];
    public bool Falhar{get;set;}
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {Corpos.Add(await request.Content!.ReadAsStringAsync(ct));return new(Falhar?HttpStatusCode.ServiceUnavailable:HttpStatusCode.Created);}
}

public class SalaoConcorrenciaTests
{
    [Fact] public async Task DuasInstanciasNaoAbremAMesmaMesa()
    {
        using var b=new BancoSalao();var arquivo=Path.Combine(Path.GetTempPath(),$"kiora-salao-{Guid.NewGuid():N}.db");
        try
        {
            using(var destino=new SqliteConnection($"Data Source={arquivo}")){destino.Open();((SqliteConnection)b.Db.Database.GetDbConnection()).BackupDatabase(destino);}
            var versao=(await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1)).Versao;
            var inicio=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Abrir(int garcom)
            {
                await inicio.Task;
                await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={arquivo};Default Timeout=10;Pooling=False").Options);
                var acesso=new AcessoSalaoService(db);var comandos=new ComandoSalaoService(db,b.Relogio,acesso);var avisos=new AvisosSalaoService(db,b.Relogio);
                var service=new ComandaService(db,acesso,comandos,b.Horarios,b.Relogio,avisos);
                try{await service.Acomodar(garcom,new(){MesaId=1,Versao=versao,ChaveSolicitacao=Guid.NewGuid(),Pessoas=2});return true;}
                catch(ValidationException){return false;}
            }
            var primeira=Task.Run(()=>Abrir(b.Garcom.Id));var segunda=Task.Run(()=>Abrir(b.OutroGarcom.Id));inicio.SetResult();
            var resultados=await Task.WhenAll(primeira,segunda);Assert.Equal(1,resultados.Count(r=>r));
            await using var conferir=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={arquivo};Pooling=False").Options);
            Assert.Single(await conferir.AtendimentosMesa.ToListAsync());Assert.Single(await conferir.MovimentosSalao.ToListAsync());
        }
        finally{SqliteConnection.ClearAllPools();File.Delete(arquivo);}
    }
    [Fact] public async Task AvisoProntoTemDonoEFuncionarioTransferidoNaoLeAvisosDaMesaAnterior()
    {
        using var b=new BancoSalao();var id=await b.Abrir();await b.Adicionar(id,1);await b.PrepararItem(id);
        var avisos=new KioraRestaurante.Services.Notificacoes.NotificacaoService(b.Db,new EphemeralDataProtectionProvider(),b.Relogio);
        var aviso=Assert.Single((await avisos.Listar(b.Garcom.Id,1)).Itens);
        Assert.Equal(id,aviso.AtendimentoId);Assert.Contains("Mesa",aviso.Titulo);
        Assert.Empty((await avisos.Listar(b.Cliente.Id,1)).Itens);Assert.Empty((await avisos.Listar(b.Portaria.Id,1)).Itens);Assert.Empty((await avisos.Listar(b.Cozinheiro.Id,1)).Itens);
        var a=await b.Detalhe(id);await b.Comandas.Transferir(b.Gerente.Id,new(){AtendimentoId=id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),GarcomId=b.OutroGarcom.Id});
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>avisos.Detalhe(b.Garcom.Id,aviso.Id));
        Assert.Single((await avisos.Listar(b.OutroGarcom.Id,1)).Itens);
    }
    [Fact] public async Task CancelamentoDeliveryDurantePreparoImpedeAcaoDaTelaAntiga()
    {
        using var b=new BancoSalao();var p=b.Delivery(true);b.Db.Add(p);await b.Db.SaveChangesAsync();
        var item=Assert.Single(await b.Cozinha.Fila(b.Cozinheiro.Id));p.StatusPedido=StatusPedido.Cancelado;p.Versao=Guid.NewGuid();await b.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(()=>b.Cozinha.Atualizar(b.Cozinheiro.Id,new(){Origem="delivery",ItemDeliveryId=item.ItemDeliveryId,Versao=item.Versao,Acao="iniciar",ChaveSolicitacao=Guid.NewGuid()}));
        Assert.Empty(await b.Cozinha.Fila(b.Cozinheiro.Id));
    }
}
