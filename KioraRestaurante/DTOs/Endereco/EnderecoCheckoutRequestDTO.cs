using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Endereco;

public class EnderecoCheckoutRequestDTO : IValidatableObject
{
    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(@"^[0-9]{5}-?[0-9]{3}$", ErrorMessage = "Informe um CEP válido.")]
    public string Cep { get; set; } = "";

    [Required(ErrorMessage = "Informe a rua ou avenida.")]
    [StringLength(150, ErrorMessage = "A rua deve ter até 150 caracteres.")]
    public string Logradouro { get; set; } = "";

    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(20, ErrorMessage = "O número deve ter até 20 caracteres.")]
    public string Numero { get; set; } = "";

    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(100, ErrorMessage = "O bairro deve ter até 100 caracteres.")]
    public string Bairro { get; set; } = "";

    [StringLength(200, ErrorMessage = "O complemento deve ter até 200 caracteres.")]
    public string? Complemento { get; set; }

    [StringLength(200, ErrorMessage = "A referência deve ter até 200 caracteres.")]
    public string? Referencia { get; set; }

    // Preencher para a entrega não cadastra o endereço automaticamente.
    public bool SalvarNaConta { get; set; }

    [StringLength(40, ErrorMessage = "O nome do endereço deve ter até 40 caracteres.")]
    public string? Apelido { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SalvarNaConta && string.IsNullOrWhiteSpace(Apelido))
            yield return new("Dê um nome ao endereço que deseja salvar.", [nameof(Apelido)]);
    }
}
