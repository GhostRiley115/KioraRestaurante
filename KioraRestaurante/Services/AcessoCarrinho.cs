namespace KioraRestaurante.Services;
/* Identifica quem está acessando o carrinho.
 - Se o visitante estiver logado, UsuarioId contém o ID do usuário.
 - Se não estiver logado, CarrinhoVisitanteId identifica o carrinho criado para esse visitante.
 Esses dados são obtidos pelo servidor através da autenticação e do cookie do visitante,
 e não devem ser enviados pelo cliente no corpo da requisição. */
public class AcessoCarrinho
{
    public int? UsuarioId { get; init; } //init -> uma vez iniciado não pode ser alterado
    public int? CarrinhoVisitanteId { get; init; }
}