using KioraRestaurante.DTOs.Carrinho;
using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Interfaces
{
    public interface ICarrinhoService
    {
        //Busca o carrinho associado ao visitante ou usuário, se não existir, cria um novo.
        Task<CarrinhoResponseDTO> BuscarCarrinho(AcessoCarrinho acesso);
        Task<CarrinhoResponseDTO> RevisarCarrinho(AcessoCarrinho acesso);
        //Adiciona um produto ao carrinho. Caso o produto já esteja no carrinho, sua quantidade deverá ser atualizada em vez de criar um novo ItemCarrinho.
        Task <CarrinhoResponseDTO> AdicionarProduto(AcessoCarrinho acesso, AdicionarProdutoCarrinhoRequestDTO dto);
        //Remove um produto do carrinho. A operação deverá localizar o ItemCarrinho correspondente ao produto e removê-lo completamente do carrinho.
        Task <CarrinhoResponseDTO> RemoverProduto(AcessoCarrinho acesso, int produtoId);
        /*Atualiza a quantidade de um produto que já está no carrinho.
        Caso a quantidade seja zero ou inválida, deverá ser aplicada a regra definida para remoção do item.*/
        Task <CarrinhoResponseDTO> AtualizarQuantidade(AcessoCarrinho acesso, int produtoId, ItemCarrinhoUpdateDTO dto);
        //Remove todos os itens do carrinho. O carrinho permanece cadastrado, mas fica sem produtos.
        Task <CarrinhoResponseDTO> EsvaziarCarrinho(AcessoCarrinho acesso);
        //Mescla o carrinho do visitante ao do usuário.
        Task<MesclagemCarrinhoResponseDTO> MesclarCarrinhos(AcessoCarrinho acesso, MesclarCarrinhoRequestDTO dto);
    }
}

