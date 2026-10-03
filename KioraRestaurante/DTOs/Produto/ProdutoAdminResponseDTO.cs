namespace KioraRestaurante.DTOs.Produto;

public class ProdutoAdminResponseDTO
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public decimal Preco { get; set; }

    public string ImagemUrl { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    public string NomeCategoria { get; set; } = string.Empty;

    public bool CategoriaAtiva { get; set; }

    public bool Ativo { get; set; }

    public bool Disponivel { get; set; }

    public bool EhCombo { get; set; }

    public decimal DescontoPercentual { get; set; }

    // Dados necessários para preencher a edição de um combo.
    public List<ComboComponenteRequestDTO> Componentes { get; set; } = new();
}