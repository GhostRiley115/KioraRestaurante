using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Services.Salao;
namespace KioraRestaurante.ViewModels;
public record PainelSalaoViewModel(List<MesaSalaoDTO> Mesas,List<GrupoSalaoDTO> Grupos,List<FuncionarioOpcaoDTO> Garcons);
public record ComandaSalaoViewModel(ComandaDetalhesDTO Comanda,List<ProdutoResponseDTO> Produtos,List<FuncionarioOpcaoDTO> Garcons);
