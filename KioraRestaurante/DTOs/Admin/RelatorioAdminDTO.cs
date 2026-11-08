using KioraRestaurante.ViewModels.Admin;
namespace KioraRestaurante.DTOs.Admin;

public class RelatorioAdminDTO
{
    public FiltroRelatorioAdmin Filtros { get; set; } = new();
    public DateTime GeradoEmUtc { get; set; }
    public ResumoVendasAdminDTO Atual { get; set; } = new();
    public ResumoVendasAdminDTO Anterior { get; set; } = new();
    public List<DiaVendasAdminDTO> Dias { get; set; } = [];
    public List<ProdutoVendidoAdminDTO> Produtos { get; set; } = [];
    public List<HoraMovimentoAdminDTO> Horas { get; set; } = [];
    public List<VendaAdminDTO> Vendas { get; set; } = [];
    public int DeliveryNaFila { get; set; }
    public int DeliveryEmPreparo { get; set; }
    public int DeliveryEmEntrega { get; set; }
    public int DeliveryAguardandoPagamento { get; set; }
    public int RevisoesFinanceiras { get; set; }
    public int MesasOcupadas { get; set; }
    public int MesasLimpeza { get; set; }
    public int MesasLivres { get; set; }
    public int GruposNaFila { get; set; }
    public int ReservasPeriodo { get; set; }
    public int ReservasCanceladas { get; set; }
    public decimal? Variacao => Anterior.Total == 0 ? null : (Atual.Total-Anterior.Total)/Anterior.Total*100;
}
public class ResumoVendasAdminDTO
{
    public decimal Delivery { get; set; }
    public decimal Salao { get; set; }
    public decimal Frete { get; set; }
    public decimal DescontoCatalogo { get; set; }
    public decimal Cupons { get; set; }
    public decimal DescontosComanda { get; set; }
    public int UnidadesPromocionais { get; set; }
    public int UnidadesSemHistoricoDesconto { get; set; }
    public int PedidosComCupom { get; set; }
    public int Quantidade { get; set; }
    public decimal Total => Delivery + Salao;
    public decimal ProdutosLiquidos => Total - Frete;
    public decimal TicketMedio => Quantidade == 0 ? 0 : Total/Quantidade;
}
public record DiaVendasAdminDTO(DateTime Dia, decimal Delivery, decimal Salao);
public record ProdutoVendidoAdminDTO(int Id, string Nome, int Quantidade, int Delivery, int Salao, int Promocionais, decimal Valor);
public record HoraMovimentoAdminDTO(int Hora, int Delivery, int Salao, int Reservas);
public class VendaAdminDTO
{
    public string Canal { get; set; } = "";
    public string Identificacao { get; set; } = "";
    public int? PedidoId { get; set; }
    public Guid? AtendimentoId { get; set; }
    public DateTime DataUtc { get; set; }
    public decimal Total { get; set; }
    public decimal Frete { get; set; }
    public decimal DescontoPedido { get; set; }
    public List<ItemVendaAdminDTO> Itens { get; set; } = [];
}
public record ItemVendaAdminDTO(int ProdutoId, string Nome, int Quantidade, decimal Preco, decimal? Original);
