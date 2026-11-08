using System.ComponentModel.DataAnnotations;
using System.Text;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KioraRestaurante.Tests;

public class RelatoriosAdminTests
{
    private static FiltroRelatorioAdmin Filtro(string canal="todos")=>new(){Inicio=new(2026,11,1),Fim=new(2026,11,30),CompararInicio=new(2026,10,1),CompararFim=new(2026,10,31),Canal=canal};
    private static RelatoriosAdminService Service(BancoSalao b)=>new(b.Db,b.Relogio);
    [Fact] public async Task SomaCanaisSemDuplicarFreteCuponsOuDescontos()
    {
        using var b=new BancoSalao();var p=b.Delivery(true);p.ValorTotal=55;p.TaxaEntrega=5;p.DescontoCupom=10;
        p.ItensPedido[0].Quantidade=2;p.ItensPedido[0].PrecoUnitario=30;p.ItensPedido[0].PrecoOriginalUnitario=40;
        b.Db.Add(p);
        b.Db.Add(new AtendimentoMesa{MesaId=1,NomeMesa="Mesa 1",GarcomId=b.Garcom.Id,NomeGrupo="Teste",AbertoEmUtc=b.Relogio.Agora.UtcDateTime.AddHours(-1),FechadoEmUtc=b.Relogio.Agora.UtcDateTime,
            Situacao=SituacaoAtendimento.Encerrado,SubtotalFechamento=30,TotalFechamento=25,
            Itens=[new(){ProdutoId=b.Produto.Id,NomeProduto="Nome histórico",Quantidade=1,PrecoUnitario=30,PrecoOriginalUnitario=40,Situacao=SituacaoPreparo.Entregue},
                new(){ProdutoId=b.Produto.Id,NomeProduto="Cancelado",Quantidade=90,PrecoUnitario=30,PrecoOriginalUnitario=40,Situacao=SituacaoPreparo.Cancelado}]});
        await b.Db.SaveChangesAsync();b.Produto.Preco=900;b.Produto.DescontoPercentual=0;await b.Db.SaveChangesAsync();
        var r=await Service(b).Obter(b.Admin.Id,Filtro());
        Assert.Equal(80,r.Atual.Total);Assert.Equal(75,r.Atual.ProdutosLiquidos);Assert.Equal(5,r.Atual.Frete);
        Assert.Equal(30,r.Atual.DescontoCatalogo);Assert.Equal(10,r.Atual.Cupons);Assert.Equal(5,r.Atual.DescontosComanda);
        Assert.Equal(3,r.Atual.UnidadesPromocionais);Assert.Equal(2,r.Atual.Quantidade);Assert.Equal(40,r.Atual.TicketMedio);
        Assert.Equal(80,r.Dias.Sum(d=>d.Delivery+d.Salao));var produto=Assert.Single(r.Produtos);Assert.Equal(3,produto.Quantidade);Assert.Equal(90,produto.Valor);
        Assert.Equal(55,(await Service(b).Obter(b.Admin.Id,Filtro("delivery"))).Atual.Total);
        Assert.Equal(25,(await Service(b).Obter(b.Admin.Id,Filtro("salao"))).Atual.Total);
    }
    [Theory]
    [InlineData(StatusPagamento.Pendente,StatusPedido.Recebido,FormaPagamento.MercadoPago,false)]
    [InlineData(StatusPagamento.Aprovado,StatusPedido.Cancelado,FormaPagamento.MercadoPago,false)]
    [InlineData(StatusPagamento.Reembolsado,StatusPedido.Entregue,FormaPagamento.MercadoPago,false)]
    [InlineData(StatusPagamento.Conferencia,StatusPedido.Entregue,FormaPagamento.MercadoPago,false)]
    [InlineData(StatusPagamento.Recusado,StatusPedido.Entregue,FormaPagamento.Dinheiro,false)]
    [InlineData(StatusPagamento.Pendente,StatusPedido.Recebido,FormaPagamento.Dinheiro,false)]
    [InlineData(StatusPagamento.Pendente,StatusPedido.Entregue,FormaPagamento.Dinheiro,true)]
    [InlineData(StatusPagamento.Aprovado,StatusPedido.Recebido,FormaPagamento.Dinheiro,false)]
    [InlineData(StatusPagamento.Aprovado,StatusPedido.Entregue,FormaPagamento.Dinheiro,true)]
    [InlineData(StatusPagamento.Aprovado,StatusPedido.Recebido,FormaPagamento.MercadoPago,true)]
    public async Task ReceitaRespeitaPagamentoEntregaECancelamento(StatusPagamento pagamento,StatusPedido status,FormaPagamento forma,bool incluir)
    {
        using var b=new BancoSalao();var p=b.Delivery(true);p.StatusPagamento=pagamento;p.StatusPedido=status;p.FormaPagamento=forma;b.Db.Add(p);await b.Db.SaveChangesAsync();
        var r=await Service(b).Obter(b.Admin.Id,Filtro());Assert.Equal(incluir?20:0,r.Atual.Total);
    }
    [Fact] public async Task AjustePendenteNaoInflaReceitaERevisaoNumericaNaoBloqueiaVenda()
    {
        using var b=new BancoSalao();var p=b.Delivery(true);p.RevisaoFinanceira=3;b.Db.Add(p);await b.Db.SaveChangesAsync();
        Assert.Equal(20,(await Service(b).Obter(b.Admin.Id,Filtro())).Atual.Total);
        p.OperacaoPendenteId=Guid.NewGuid();await b.Db.SaveChangesAsync();Assert.Equal(0,(await Service(b).Obter(b.Admin.Id,Filtro())).Atual.Total);
    }
    [Fact] public async Task ViradaDeDiaUsaBrasiliaEPreencheDiasSemMovimento()
    {
        using var b=new BancoSalao();var a=b.Delivery(true);a.DataPedido=new(2026,11,1,2,59,59,DateTimeKind.Utc);
        var d=b.Delivery(true);d.DataPedido=new(2026,11,1,3,0,0,DateTimeKind.Utc);
        var fim=b.Delivery(true);fim.DataPedido=new(2026,12,1,3,0,0,DateTimeKind.Utc);b.Db.AddRange(a,d,fim);await b.Db.SaveChangesAsync();
        var r=await Service(b).Obter(b.Admin.Id,Filtro());Assert.Equal(20,r.Atual.Total);Assert.Equal(20,r.Anterior.Total);
        Assert.Equal(30,r.Dias.Count);Assert.Equal(20,r.Dias[0].Delivery);Assert.All(r.Dias.Skip(1),v=>Assert.Equal(0,v.Delivery));Assert.Equal(1,r.Horas[0].Delivery);
    }
    [Fact] public async Task ReservaUsaHoraAgendadaExcluiCanceladasEFila()
    {
        using var b=new BancoSalao();foreach(var situacao in new[]{SituacaoReserva.AguardandoChegada,SituacaoReserva.Concluida,SituacaoReserva.Cancelada})
            b.Db.Add(new Reserva{ChaveSolicitacao=Guid.NewGuid(),Nome="Teste",Telefone="11900000000",Pessoas=2,Tipo=TipoReserva.Agendada,Situacao=situacao,InicioUtc=new(2026,11,7,22,0,0,DateTimeKind.Utc),CriadaEmUtc=new(2026,10,30,10,0,0,DateTimeKind.Utc)});
        b.Db.Add(new Reserva{ChaveSolicitacao=Guid.NewGuid(),Nome="Fila",Telefone="11900000000",Pessoas=2,Tipo=TipoReserva.Fila,InicioUtc=new(2026,11,7,22,0,0,DateTimeKind.Utc)});await b.Db.SaveChangesAsync();
        var r=await Service(b).Obter(b.Admin.Id,Filtro("delivery"));Assert.Equal(2,r.ReservasPeriodo);Assert.Equal(1,r.ReservasCanceladas);Assert.Equal(2,r.Horas[19].Reservas);Assert.Equal(2,r.Horas.Sum(h=>h.Reservas));
    }
    [Fact] public async Task HistoricoSemPrecoOriginalNaoInventaPromocao()
    {
        using var b=new BancoSalao();var p=b.Delivery(true);b.Db.Add(p);b.Produto.DescontoPercentual=30;await b.Db.SaveChangesAsync();
        var r=await Service(b).Obter(b.Admin.Id,Filtro());Assert.Equal(1,r.Atual.UnidadesSemHistoricoDesconto);Assert.Equal(0,r.Atual.UnidadesPromocionais);Assert.Equal(20,r.Atual.Total);
    }
    [Fact] public async Task NovoItemPresencialPreservaDescontoMesmoAoCorrigirQuantidade()
    {
        using var b=new BancoSalao();b.Produto.Preco=40;b.Produto.DescontoPercentual=25;await b.Db.SaveChangesAsync();var id=await b.Abrir();await b.Adicionar(id,2);
        var a=await b.Detalhe(id);var item=await b.Db.ItensComanda.SingleAsync();Assert.Equal(40,item.PrecoOriginalUnitario);Assert.Equal(25,item.DescontoPercentual);Assert.Equal(30,item.PrecoUnitario);
        b.Produto.Preco=100;b.Produto.DescontoPercentual=0;await b.Db.SaveChangesAsync();
        await b.Comandas.Corrigir(b.Garcom.Id,new(){AtendimentoId=id,ItemId=item.Id,Versao=a.Versao,ChaveSolicitacao=Guid.NewGuid(),Quantidade=3,Motivo="Corrigir quantidade"});
        var novo=await b.Db.ItensComanda.SingleAsync(i=>i.Situacao!=SituacaoPreparo.Cancelado);Assert.Equal(40,novo.PrecoOriginalUnitario);Assert.Equal(25,novo.DescontoPercentual);Assert.Equal(30,novo.PrecoUnitario);
    }
    [Fact] public async Task PadraoComparaMesAnteriorAteOMesmoDiaESemDivisaoPorZero()
    {
        using var b=new BancoSalao();var r=await Service(b).Obter(b.Admin.Id,new());Assert.Equal(new DateTime(2026,11,1),r.Filtros.Inicio);Assert.Equal(new DateTime(2026,11,7),r.Filtros.Fim);Assert.Equal(new DateTime(2026,10,7),r.Filtros.CompararFim);Assert.Null(r.Variacao);Assert.Equal(0,r.Atual.TicketMedio);
    }
    [Theory] [InlineData("salao",400)] [InlineData("invalido",10)] [InlineData("todos",-1)]
    public async Task FiltroInvalidoNaoExecutaRelatorio(string canal,int dias)
    {
        using var b=new BancoSalao();var f=Filtro(canal);f.Fim=f.Inicio!.Value.AddDays(dias);
        await Assert.ThrowsAsync<ValidationException>(()=>Service(b).Obter(b.Admin.Id,f));
    }
    [Fact] public async Task AdministradorDesativadoECargosOperacionaisNaoAcessamDados()
    {
        using var b=new BancoSalao();foreach(var u in b.Usuarios.Where(u=>u.Tipo!=TipoUsuario.Administrador))
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(b).Obter(u.Id,Filtro()));
        b.Admin.Ativo=false;await b.Db.SaveChangesAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(b).Obter(b.Admin.Id,Filtro()));
    }
    [Fact] public async Task PedidosSeparamAtivosDoHistoricoEMantemBuscaGlobal()
    {
        using var b=new BancoSalao();foreach(var s in Enum.GetValues<StatusPedido>()){var p=b.Delivery(true);p.StatusPedido=s;b.Db.Add(p);}await b.Db.SaveChangesAsync();
        var service=new AdminService(b.Db);var ativos=await service.Pedidos(b.Admin.Id,new());Assert.Equal(3,ativos.Total);Assert.Equal(new[]{StatusPedido.Recebido,StatusPedido.EmPreparo,StatusPedido.SaiuParaEntrega},ativos.Itens.Select(p=>p.StatusPedido));var h=await service.Pedidos(b.Admin.Id,new(){Aba="historico"});Assert.Equal(2,h.Total);Assert.All(h.Itens,p=>Assert.True(p.StatusPedido is StatusPedido.Entregue or StatusPedido.Cancelado));Assert.Equal(5,(await service.Pedidos(b.Admin.Id,new(){Aba="todos"})).Total);
    }
    [Fact] public async Task MesaChamadaPelaPortariaNaoApareceLivreEAbertaNaoContaComoVenda()
    {
        using var b=new BancoSalao();var codigo=await b.PortariaService.Fila(b.Portaria.Id,new(){Nome="Grupo",Pessoas=2,ChaveSolicitacao=Guid.NewGuid()});
        await b.PortariaService.Chamar(b.Portaria.Id,1,Guid.NewGuid());var r=await Service(b).Obter(b.Admin.Id,Filtro());Assert.Equal(11,r.MesasLivres);Assert.Equal(1,r.GruposNaFila);
        var mesa=await b.Db.MesasRestaurante.SingleAsync(m=>m.Id==1);
        await b.Comandas.Acomodar(b.Garcom.Id,new(){MesaId=1,Versao=mesa.Versao,ReservaCodigo=codigo,ChaveSolicitacao=Guid.NewGuid()});
        r=await Service(b).Obter(b.Admin.Id,Filtro());Assert.Equal(11,r.MesasLivres);Assert.Equal(1,r.MesasOcupadas);Assert.Equal(0,r.Atual.Total);
    }
    [Fact] public void CsvProtegeFormulasEAspasSemPerderMoedaBrasileira()
    {
        var csv=Encoding.UTF8.GetString(CsvVendasAdmin.Gerar([new(){Canal="salao",Identificacao=" =HYPERLINK(\"x\")",Total=12.5m,DataUtc=new(2026,11,7,15,0,0,DateTimeKind.Utc)}]));
        Assert.Contains("' =HYPERLINK(\"\"x\"\")",csv);Assert.Contains("12,50",csv);Assert.Contains("07/11/2026 12:00",csv);
    }
}
