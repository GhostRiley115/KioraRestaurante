using KioraRestaurante.DTOs.Produto;
using Microsoft.AspNetCore.Http;
using KioraRestaurante.DTOs.Admin;

namespace KioraRestaurante.Services.Interfaces
{
    public interface IProdutoService
    {
        Task<int> Criar(int administradorId, CriarProdutoRequestDTO dto, IFormFile foto);
        Task<List<ProdutoResponseDTO>> ListarCardapio(int? categoriaId = null);
        Task<ResultadoPaginadoDTO<ProdutoAdminResponseDTO>> ListarAdmin(int administradorId, FiltroProdutosAdminDTO filtro);
        Task<ProdutoAdminResponseDTO?> BuscarAdmin(int administradorId, int produtoId);
        Task Editar(int administradorId, int produtoId, EditarProdutoRequestDTO dto, IFormFile? foto);
        Task AlterarAtivo(int administradorId, int produtoId, bool ativo);
        Task AlterarDisponibilidade(int administradorId, int produtoId, bool disponivel);
        Task AlterarDesconto(int administradorId, int produtoId, AlterarDescontoRequestDTO dto);
        Task<List<OpcaoComponenteDTO>> ListarOpcoesComponentes(int administradorId, int? produtoAtualId = null);
    }
}