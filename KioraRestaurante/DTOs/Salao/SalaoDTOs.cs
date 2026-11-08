using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Models.Enums;
namespace KioraRestaurante.DTOs.Salao;

public class ComandoSalaoDTO { public Guid ChaveSolicitacao { get; set; } public Guid Versao { get; set; } }
public class AcomodarMesaDTO : ComandoSalaoDTO
{
    public int MesaId { get; set; }
    public Guid? ReservaCodigo { get; set; }
    [StringLength(100)] public string? NomeGrupo { get; set; }
    [Range(1, 30)] public int Pessoas { get; set; } = 2;
    public int? GarcomId { get; set; }
}
public class ComandaComandoDTO : ComandoSalaoDTO { public Guid AtendimentoId { get; set; } }
public class ItemComandaNovoDTO
{
    [Range(1,int.MaxValue)] public int ProdutoId { get; set; }
    [Range(1,30)] public int Quantidade { get; set; } = 1;
    [StringLength(500)] public string? Observacao { get; set; }
}
public class AdicionarRodadaDTO : ComandaComandoDTO { public List<ItemComandaNovoDTO> Itens { get; set; } = []; }
public class CorrigirItemDTO : ComandaComandoDTO
{
    public Guid ItemId { get; set; }
    public int? ProdutoId { get; set; }
    [Range(0,30)] public int Quantidade { get; set; }
    [StringLength(500)] public string? Observacao { get; set; }
    [Required(ErrorMessage="Informe o motivo da correção."),StringLength(400)] public string Motivo { get; set; } = "";
}
public class EntregarItemDTO : ComandaComandoDTO { public Guid ItemId { get; set; } }
public class TransferirMesaDTO : ComandaComandoDTO { public int GarcomId { get; set; } }
public class DescontoComandaDTO : ComandaComandoDTO
{
    [EnumDataType(typeof(TipoDescontoPresencial))] public TipoDescontoPresencial Tipo { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(KioraRestaurante.ModelBinding.ValorCupomModelBinder))] public decimal Valor { get; set; }
    [Required,StringLength(400)] public string Motivo { get; set; } = "";
}
public class RecebimentoPresencialDTO
{
    [EnumDataType(typeof(MeioPagamentoPresencial))] public MeioPagamentoPresencial Meio { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(KioraRestaurante.ModelBinding.ValorCupomModelBinder))] public decimal Valor { get; set; }
    [StringLength(100)] public string? Referencia { get; set; }
}
public class FecharComandaDTO : ComandaComandoDTO
{
    public bool PagamentoExternoConferido { get; set; }
    public List<RecebimentoPresencialDTO> Pagamentos { get; set; } = [];
}
public class ConfigurarMesaDTO : ComandoSalaoDTO
{
    public int MesaId { get; set; }
    [Required,StringLength(30)] public string Nome { get; set; } = "";
    [Range(1,30)] public int Capacidade { get; set; }
    public bool Ativa { get; set; } = true;
}
public class FilaPresencialDTO : ComandoSalaoDTO
{
    [Required,StringLength(100,MinimumLength=2)] public string Nome { get; set; } = "";
    [Range(1,30)] public int Pessoas { get; set; } = 2;
    [StringLength(25)] public string? Telefone { get; set; }
}
public class ChegadaReservaDTO : ComandoSalaoDTO { public Guid Codigo { get; set; } }
public class PreparoItemDTO : ComandoSalaoDTO
{
    [Required] public string Origem { get; set; } = "";
    public Guid? ItemSalaoId { get; set; }
    public int? ItemDeliveryId { get; set; }
    [Required] public string Acao { get; set; } = "";
}
public record MesaSalaoDTO(int Id,string Nome,int Capacidade,bool Habilitada,string Situacao,Guid Versao,
    Guid? AtendimentoId,string? Garcom,int? GarcomId,int? Pessoas,DateTime? AbertoEmUtc);
public record FuncionarioOpcaoDTO(int Id,string Nome,TipoUsuario Cargo);
public record ItemComandaDTO(Guid Id,int ProdutoId,string Nome,int Quantidade,decimal PrecoUnitario,string? Observacao,
    string? Composicao,string? ImagemUrl,SituacaoPreparo Situacao,DateTime EnviadoEmUtc,Guid Versao,string? MotivoCancelamento);
public record HistoricoSalaoDTO(DateTime DataUtc,string Responsavel,string Descricao);
public record ComandaDetalhesDTO(Guid Id,int MesaId,string Mesa,string NomeGrupo,int Pessoas,int GarcomId,string Garcom,
    SituacaoAtendimento Situacao,DateTime AbertoEmUtc,DateTime? FechadoEmUtc,Guid Versao,decimal Subtotal,decimal Desconto,
    decimal Total,string? MotivoDesconto,List<ItemComandaDTO> Itens,List<HistoricoSalaoDTO> Historico,List<RecebimentoPresencialDTO> Pagamentos);
public record ItemCozinhaDTO(string Origem,Guid? ItemSalaoId,int? ItemDeliveryId,Guid? AtendimentoId,int? PedidoId,string Destino,
    string Nome,int Quantidade,string? Observacao,string? Composicao,SituacaoPreparo Situacao,DateTime EnviadoEmUtc,Guid Versao);
public record TotaisDiaSalaoDTO(DateOnly Dia,int AtendimentosIniciados,int AtendimentosNoDia,int MesasDistintas,int ContasFechadas,decimal Produtos,
    decimal Descontos,decimal Vendas,int OcupadasAgora,List<ResumoContaSalaoDTO> Contas);
public record ResumoContaSalaoDTO(Guid Id,string Mesa,string Garcom,DateTime FechadoEmUtc,decimal Subtotal,decimal Desconto,decimal Total);
public record ComparacaoSalaoDTO(TotaisDiaSalaoDTO Atual,TotaisDiaSalaoDTO Comparado);
public class ChamarGrupoDTO : ComandoSalaoDTO { public int MesaId { get; set; } }
public class CancelarGrupoDTO : ChegadaReservaDTO { [Required,StringLength(400)] public string Motivo { get; set; } = ""; }
