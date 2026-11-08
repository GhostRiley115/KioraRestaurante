using System.ComponentModel.DataAnnotations;
namespace KioraRestaurante.DTOs.Admin;
public class CancelarPedidoAdminRequestDTO
{
    [Range(1,int.MaxValue)] public int PedidoId { get; set; }
    public Guid VersaoPedido { get; set; }
    [Required(ErrorMessage="Informe o motivo do cancelamento."),StringLength(300,ErrorMessage="Use até 300 caracteres no motivo.")]
    public string Motivo { get; set; } = "";
}
public record ResultadoCancelamentoAdmin(bool Concluido,bool ReembolsoPendente);
