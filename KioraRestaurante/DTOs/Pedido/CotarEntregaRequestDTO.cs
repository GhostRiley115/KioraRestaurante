using System.ComponentModel.DataAnnotations;
using KioraRestaurante.DTOs.Endereco;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.DTOs.Pedido;

public class CotarEntregaRequestDTO : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Escolha um endereço válido.")]
    public int? EnderecoId { get; set; }
    public Guid? EnderecoVersao { get; set; }

    // Só valida os campos do novo endereço quando ele é a opção escolhida.
    [ValidateNever]
    public EnderecoCheckoutRequestDTO? NovoEndereco { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EnderecoId.HasValue)
        {
            if (!EnderecoVersao.HasValue || EnderecoVersao == Guid.Empty)
                yield return new("Atualize a revisão do endereço.", [nameof(EnderecoVersao)]);
            yield break;
        }

        if (NovoEndereco == null)
        {
            yield return new("Preencha o endereço da entrega.", [nameof(NovoEndereco)]);
            yield break;
        }

        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(NovoEndereco, new(NovoEndereco), erros, true);
        foreach (var erro in erros)
            yield return new(erro.ErrorMessage, erro.MemberNames.Select(n => $"{nameof(NovoEndereco)}.{n}"));
    }

    public CotarEntregaRequestDTO SomenteEndereco() => new()
    {
        EnderecoId = EnderecoId, EnderecoVersao = EnderecoVersao, NovoEndereco = NovoEndereco
    };
}
