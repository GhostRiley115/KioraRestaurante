namespace KioraRestaurante.DTOs.Admin;

// Os detalhes usam o nome e o preço registrados na compra, e não o preço atual do cardápio.
public class PedidoDetalhesAdminDTO : PedidoAdminDTO
{
    public bool ClienteAtivo { get; set; }
    public List<ItemPedidoAdminDTO> Itens { get; set; } = new();
    public EnderecoPedidoAdminDTO? Endereco { get; set; }
}
public class ItemPedidoAdminDTO
{
    public string Nome { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
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
