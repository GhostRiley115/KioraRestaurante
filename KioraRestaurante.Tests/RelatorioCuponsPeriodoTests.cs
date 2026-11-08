using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services;
using KioraRestaurante.ViewModels.Admin;
using Xunit;
namespace KioraRestaurante.Tests;
public class RelatorioCuponsPeriodoTests
{
    private static FiltroRelatorioAdmin Periodo(string canal="todos")=>new(){Inicio=new(2026,11,1),Fim=new(2026,11,7),Canal=canal};
    [Fact] public async Task AgrupaUsosPorCodigoDataDoPedidoSemSomarQuantidadeDeProdutos()
    {
        using var b=new BancoSalao();b.Db.Cupons.AddRange(new Cupom{Codigo="PRIMEIRACOMPRA",Descricao="Primeira",CriadoPorId=b.Admin.Id,CriadoEmUtc=new(2026,10,1),InicioEmUtc=new(2026,10,1),FimEmUtc=new(2026,12,1)},new Cupom{Codigo="BLACKFRIDAY",Descricao="Campanha",CriadoPorId=b.Admin.Id,CriadoEmUtc=new(2026,10,1),InicioEmUtc=new(2026,10,1),FimEmUtc=new(2026,12,1)});
        for(int i=0;i<6;i++){var p=b.Delivery(true);p.CodigoCupom=i<2?"PRIMEIRACOMPRA":"BLACKFRIDAY";p.DescontoCupom=5;p.ValorTotal=15;p.ItensPedido[0].Quantidade=10;
            if(i==3)p.StatusPedido=StatusPedido.Cancelado;if(i==4)p.StatusPagamento=StatusPagamento.Pendente;if(i==5)p.DataPedido=new(2026,10,20,15,0,0,DateTimeKind.Utc);b.Db.Add(p);}
        await b.Db.SaveChangesAsync();var r=await new RelatoriosAdminService(b.Db,b.Relogio).Obter(b.Admin.Id,Periodo());
        Assert.Equal(2,r.Cupons.Count);Assert.Equal(2,r.Cupons.Single(c=>c.Codigo=="PRIMEIRACOMPRA").Utilizados);Assert.Equal(1,r.Cupons.Single(c=>c.Codigo=="BLACKFRIDAY").Utilizados);
        Assert.Equal(15,r.Cupons.Sum(c=>c.Descontos));Assert.Equal(r.Atual.PedidosComCupom,r.Cupons.Sum(c=>c.Utilizados));Assert.Equal(r.Atual.Cupons,r.Cupons.Sum(c=>c.Descontos));
        Assert.Empty((await new RelatoriosAdminService(b.Db,b.Relogio).Obter(b.Admin.Id,Periodo("salao"))).Cupons);
    }
    [Fact] public async Task MostraCampanhaSemUsoNoPeriodoEPreservaCodigoHistoricoSemCadastroAtual()
    {
        using var b=new BancoSalao();b.Db.Cupons.Add(new(){Codigo="SEMUSO",Descricao="Teste",CriadoPorId=b.Admin.Id,CriadoEmUtc=new(2026,10,1),InicioEmUtc=new(2026,10,1),FimEmUtc=new(2026,12,1)});
        var p=b.Delivery(true);p.CodigoCupom="ANTIGO";p.DescontoCupom=3;b.Db.Add(p);await b.Db.SaveChangesAsync();
        var r=await new RelatoriosAdminService(b.Db,b.Relogio).Obter(b.Admin.Id,Periodo());Assert.Equal(0,r.Cupons.Single(c=>c.Codigo=="SEMUSO").Utilizados);var antigo=r.Cupons.Single(c=>c.Codigo=="ANTIGO");Assert.Equal(1,antigo.Utilizados);Assert.Null(antigo.Id);
        var f=Periodo();f.Inicio=new(2026,11,8);f.Fim=new(2026,11,8);r=await new RelatoriosAdminService(b.Db,b.Relogio).Obter(b.Admin.Id,f);Assert.Equal(0,r.Cupons.Sum(c=>c.Utilizados));
    }
}
