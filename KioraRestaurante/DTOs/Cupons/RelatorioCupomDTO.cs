using KioraRestaurante.Models.Cupons;
namespace KioraRestaurante.DTOs.Cupons;
public record UsoCupomRelatorio(int PedidoId, int UsuarioId, string? Parceiro, decimal Desconto, SituacaoUsoCupom Situacao,
    DateTime ReservadoEmUtc, DateTime? ConfirmadoEmUtc, DateTime? CanceladoEmUtc);
public record RelatorioCupomDTO(SalvarCupomDTO Cupom, int Reservados, int Confirmados, int Cancelados,
    decimal DescontosConfirmados, List<UsoCupomRelatorio> Usos, int Pagina, int TotalPaginas);
