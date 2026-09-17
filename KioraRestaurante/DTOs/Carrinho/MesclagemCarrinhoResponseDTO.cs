namespace KioraRestaurante.DTOs.Carrinho;

public class MesclagemCarrinhoResponseDTO
{
    //Responde se a mesclagem terminou -> Se não terminou, restam conflitos a serem resolvidos.
    public bool Concluida { get; set; }
    //Retorna um carrinho sem conflitos, caso a mesclagem seja bem sucedida.
    public CarrinhoResponseDTO? Carrinho { get; set; }
    //Quantidade de conflitos a serem resolvidos -> se for 0 a mesclagem pode continuar.
    public List<ConflitoMesclagemDTO> Conflitos { get; set; } = new();
}