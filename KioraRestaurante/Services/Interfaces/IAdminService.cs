using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.ViewModels.Admin;
namespace KioraRestaurante.Services.Interfaces;

public interface IAdminService
{
    Task<PainelAdminDTO> Painel(int administradorId);
    Task<ResultadoPaginadoDTO<UsuarioAdminDTO>> Usuarios(int administradorId, FiltroUsuariosAdmin filtro);
    Task<ResultadoPaginadoDTO<PedidoAdminDTO>> Pedidos(int administradorId, FiltroPedidosAdmin filtro);
    Task<PedidoDetalhesAdminDTO?> DetalhesPedido(int administradorId, int pedidoId);
    Task AlterarSituacaoUsuario(int administradorId, int usuarioId, bool ativo);
}
