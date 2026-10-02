using KioraRestaurante.DTOs.Admin;
namespace KioraRestaurante.ViewModels.Admin;

// Cada página leva os filtros preenchidos e o resultado da consulta.
public class UsuariosAdminViewModel
{
    public FiltroUsuariosAdmin Filtros { get; set; } = new();
    public ResultadoPaginadoDTO<UsuarioAdminDTO> Resultado { get; set; } = new();
}
public class PedidosAdminViewModel
{
    public FiltroPedidosAdmin Filtros { get; set; } = new();
    public ResultadoPaginadoDTO<PedidoAdminDTO> Resultado { get; set; } = new();
}
public class PaginacaoAdminViewModel
{
    public int Pagina { get; set; }
    public int TotalPaginas { get; set; }
    public string Acao { get; set; } = "";
    public Dictionary<string, string> Filtros { get; set; } = new();
}
