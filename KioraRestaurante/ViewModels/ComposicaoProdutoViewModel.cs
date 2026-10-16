using KioraRestaurante.DTOs.Produto;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class ComposicaoProdutoViewModel
{
    public bool EhCombo { get; set; }

    // Componentes escolhidos pelo administrador.
    public List<ComboComponenteRequestDTO> Componentes { get; set; } = new();

    // Opções carregadas pelo servidor para preencher os selects.
    // Não confiamos em nomes enviados pelo navegador.
    [BindNever]
    [ValidateNever]
    public List<OpcaoComponenteDTO> Opcoes { get; set; } = new();
}