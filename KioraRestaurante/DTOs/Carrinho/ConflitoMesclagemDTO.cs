namespace KioraRestaurante.DTOs.Carrinho;

/*Impede o conflito de um carrinho visitante + carrinho usuário
ultrapassar o limite máximo de unidade do produto*/
public class ConflitoMesclagemDTO
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public int QuantidadeVisitante { get; set; }
    public int QuantidadeUsuario { get; set; }
    public int QuantidadeSomada { get; set; }
}