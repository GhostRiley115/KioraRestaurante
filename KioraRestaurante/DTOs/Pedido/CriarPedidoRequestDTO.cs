using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Enums;

namespace KioraRestaurante.DTOs.Pedido;

public class CriarPedidoRequestDTO
{
    [Required(ErrorMessage = "Atualize a revisão do pedido.")]
    public string TokenRevisao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha a forma de pagamento.")]
    [EnumDataType(
        typeof(FormaPagamento),
        ErrorMessage = "Forma de pagamento inválida.")]
    public FormaPagamento? FormaPagamento { get; set; }

    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(
        @"^\d{5}-?\d{3}$",
        ErrorMessage = "Informe um CEP como 12345-678.")]
    public string Cep { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a rua ou avenida.")]
    [StringLength(150)]
    public string Logradouro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o estado.")]
    [RegularExpression(
        "^(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)$",
        ErrorMessage = "Selecione um estado válido.")]
    public string Uf { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Complemento { get; set; }

    [StringLength(200)]
    public string? Referencia { get; set; }
}