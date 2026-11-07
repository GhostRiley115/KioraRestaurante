using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Cupons;

namespace KioraRestaurante.DTOs.Cupons;

public class SalvarCupomDTO
{
    public int? Id { get; set; }
    public Guid Versao { get; set; }
    [Required(ErrorMessage = "Informe o código do cupom."), StringLength(40, ErrorMessage = "Use no máximo 40 caracteres no código.")] public string Codigo { get; set; } = "";
    [Required(ErrorMessage = "Informe uma descrição."), StringLength(200, ErrorMessage = "Use no máximo 200 caracteres na descrição.")] public string Descricao { get; set; } = "";
    [StringLength(100, ErrorMessage = "Use no máximo 100 caracteres no parceiro.")] public string? Parceiro { get; set; }
    [EnumDataType(typeof(TipoDescontoCupom), ErrorMessage = "Selecione um tipo de desconto válido.")] public TipoDescontoCupom Tipo { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(KioraRestaurante.ModelBinding.ValorCupomModelBinder))]
    public decimal Valor { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(KioraRestaurante.ModelBinding.ValorCupomModelBinder))]
    public decimal ValorMinimoProdutos { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(KioraRestaurante.ModelBinding.ValorCupomModelBinder))]
    public decimal? DescontoMaximo { get; set; }
    public DateTime InicioLocal { get; set; }
    public DateTime FimLocal { get; set; }
    [Range(1, 1000000000, ErrorMessage = "O limite total deve ser positivo.")] public int? LimiteTotal { get; set; }
    [Range(1, 1000000, ErrorMessage = "Informe um limite por cliente entre 1 e 1 milhão.")] public int LimitePorCliente { get; set; } = 1;
    public bool Ativo { get; set; } = true;
}

public record CupomAplicado(int CupomId, string Codigo, string? Parceiro, Guid Versao, decimal Desconto,
    string ProdutosHash, string Token, DateTime ExpiraEmUtc);

public class CupomResumoAdminDTO
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string? Parceiro { get; set; }
    public TipoDescontoCupom Tipo { get; set; }
    public decimal Valor { get; set; }
    public bool Ativo { get; set; }
    public DateTime FimEmUtc { get; set; }
    public int? LimiteTotal { get; set; }
    public int LimitePorCliente { get; set; }
    public int Reservas { get; set; }
    public int Confirmados { get; set; }
    public int Cancelados { get; set; }
    public decimal DescontoConfirmado { get; set; }
}
