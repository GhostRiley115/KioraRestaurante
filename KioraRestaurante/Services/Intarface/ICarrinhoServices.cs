using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Intarface
{
    public interface ICarrinhoServices
    {
        //Busca um carrinho pelo seu ID. Utilizado para recuperar um carrinho específico e seus itens.
        Task<Carrinho?> BuscarCarrinho(int carrinhoId);

        //Busca o carrinho associado a um usuário. Será utilizado para recuperar o carrinho salvo do usuário após o login.
        Task<ItemCarrinhoResponseDTO?> BuscarCarrinhoUsuario(int usuarioId);

        //Adiciona um produto ao carrinho. Caso o produto já esteja no carrinho, sua quantidade deverá ser atualizada em vez de criar um novo ItemCarrinho.
        Task <ItemCarrinhoResponseDTO> AdicionarProduto(int usuarioId, ItemCarrinhoCreateDTO dto);

        //Remove um produto do carrinho. A operação deverá localizar o ItemCarrinho correspondente ao produto e removê-lo completamente do carrinho.
        Task <ItemCarrinhoResponseDTO> RemoverProduto(int usuarioId, int produtoId);

        /*Atualiza a quantidade de um produto que já está no carrinho.
        Caso a quantidade seja zero ou inválida, deverá ser aplicada a regra definida para remoção do item.*/
        Task <ItemCarrinhoResponseDTO> AtualizarQuantidade(int usuarioID, int produtoId, ItemCarrinhoUpdateDTO dto);

        //Remove todos os itens do carrinho. O carrinho permanece cadastrado, mas fica sem produtos.
        Task <ItemCarrinhoUpdateDTO> EsvaziarCarrinho(int usuarioId);

        /*Mescla o carrinho temporário do visitante com o carrinho já existente do usuário após o login.
        Os produtos presentes apenas no carrinho do visitante serão adicionados ao carrinho do usuário.
        Caso o mesmo produto esteja nos dois carrinhos, suas quantidades serão somadas. 
        Após a mesclagem, o carrinho do visitante deverá deixar de ser utilizado.*/
        Task MesclarCarrinhos(int carrinhoVisitanteId, int carrinhoUsuarioId);

        //Calcula o subtotal de um ItemCarrinho. O cálculo será baseado na quantidade do item multiplicada pelo preço do produto.
        decimal CalcularSubtotal(ItemCarrinho item);

        /*Calcula o valor total do carrinho. O total será obtido através da soma dos subtotais de todos os
        ItemCarrinho presentes no carrinho.*/
        decimal CalcularTotal(Carrinho carrinho);
    }
}
