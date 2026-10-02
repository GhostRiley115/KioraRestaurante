namespace KioraRestaurante.DTOs.Admin;

// A consulta busca somente uma página; não carrega a tabela inteira em memória.
public class ResultadoPaginadoDTO<T>
{
    public List<T> Itens { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 20;
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)TamanhoPagina));
}
