namespace KioraRestaurante.DTOs.Admin;

// Contadores gerais. Pedidos não representam necessariamente pagamentos recebidos.
public class PainelAdminDTO
{
    public int UsuariosAtivos { get; set; }
    public int UsuariosInativos { get; set; }
    public int TotalPedidos { get; set; }
    public int PedidosEmAndamento { get; set; }
}
