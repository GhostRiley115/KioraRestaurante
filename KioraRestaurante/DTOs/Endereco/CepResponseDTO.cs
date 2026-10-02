namespace KioraRestaurante.DTOs.Endereco;

// Informações obtidas pelo CEP.
public class CepResponseDTO
{
    public string Cep { get; set; } = string.Empty;
    public string Logradouro { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string Ibge { get; set; } = string.Empty;
}