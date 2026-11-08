using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Entrega;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KioraRestaurante.ViewModels;

public class EnderecoFormularioViewModel
{
    public CotarEntregaRequestDTO Dados { get; set; } = new();
    public List<EnderecoUsuario> Enderecos { get; set; } = [];
    public CotacaoEntrega? Entrega { get; set; }
}

public class AlterarEnderecoPedidoViewModel
{
    public AlterarEnderecoPedidoRequestDTO Dados { get; set; } = new();
    [BindNever, ValidateNever] public Pedido Pedido { get; set; } = new();
    [BindNever, ValidateNever] public List<EnderecoUsuario> Enderecos { get; set; } = [];
    [BindNever, ValidateNever] public CotacaoEntrega? Entrega { get; set; }
}
