namespace KioraRestaurante.DTOs.Admin;

// Os detalhes usam o nome e o preço registrados na compra, e não o preço atual do cardápio.
public class PedidoDetalhesAdminDTO : PedidoAdminDTO
{
    public Guid Versao { get; set; }
    public string? CodigoCupom { get; set; }
    public decimal DescontoCupom { get; set; }
    public bool ClienteAtivo { get; set; }
    public decimal TaxaEntrega { get; set; }
    public int? DistanciaEntregaMetros { get; set; }
    public int? DuracaoPercursoSegundos { get; set; }
    public int? PreparoMinutos { get; set; }
    public DateTime? CanceladoEmUtc { get; set; }
    public string? MotivoCancelamento { get; set; }
    public bool ReembolsoCancelamentoPendente { get; set; }
    public Guid? OperacaoPendenteId { get; set; }
    public List<HistoricoPedidoAdminDTO> Operacoes { get; set; } = [];
    public List<string> HistoricoAlteracoes { get; set; } = [];
    public List<ItemPedidoAdminDTO> Itens { get; set; } = new();
    public EnderecoPedidoAdminDTO? Endereco { get; set; }
}
public class ItemPedidoAdminDTO
{
    public string? ImagemUrl { get; set; }
    public string Nome { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal? PrecoOriginalUnitario { get; set; }
    public decimal? DescontoPercentual { get; set; }
    public KioraRestaurante.Models.Salao.SituacaoPreparo SituacaoPreparo { get; set; }
    public string? Composicao { get; set; }
    public string? Observacao { get; set; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
}
public class EnderecoPedidoAdminDTO
{
    public string Logradouro { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Bairro { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Cep { get; set; } = "";
    public string? Complemento { get; set; }
    public string? Referencia { get; set; }
}

public class HistoricoPedidoAdminDTO
{
    public KioraRestaurante.Models.TipoOperacaoPedido Tipo { get; set; }
    public KioraRestaurante.Models.EstadoOperacaoPedido Estado { get; set; }
    public DateTime CriadaEmUtc { get; set; }
    public DateTime? ConcluidaEmUtc { get; set; }
    public int? AdministradorId { get; set; }
    public string? AdministradorNome { get; set; }
    public string? Motivo { get; set; }
    public string Mensagem { get; set; } = "";
}
