using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Endereco;

public class SalvarEnderecoRequestDTO
{
    [Required(ErrorMessage = "Dê um nome ao endereço.")]
    [StringLength(40)]
    public string Apelido { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^[0-9]{5}-?[0-9]{3}$",
        ErrorMessage = "Informe um CEP válido.")]
    public string Cep { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a rua ou avenida.")]
    [StringLength(150)]
    public string Logradouro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o número ou S/N.")]
    [StringLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Complemento { get; set; }

    [StringLength(200)]
    public string? Referencia { get; set; }

    // Só é utilizada ao editar um endereço existente.
    public Guid? Versao { get; set; }
}