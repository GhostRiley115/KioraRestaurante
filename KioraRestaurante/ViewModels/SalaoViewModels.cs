using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Services.Salao;
namespace KioraRestaurante.ViewModels;
public record PainelSalaoViewModel(List<MesaSalaoDTO> Mesas,List<GrupoSalaoDTO> Grupos,List<FuncionarioOpcaoDTO> Garcons) { public DateOnly? DataConsulta { get; init; } }
public record ComandaSalaoViewModel(ComandaDetalhesDTO Comanda,List<ProdutoResponseDTO> Produtos,List<FuncionarioOpcaoDTO> Garcons);

public record ConfigurarMesasViewModel(ConfigurarMesaDTO Edicao,List<MesaSalaoDTO> Mesas) { public bool Bloqueada=>Edicao.MesaId>0&&Mesas.Any(m=>m.Id==Edicao.MesaId&&(m.AtendimentoId.HasValue||m.Situacao=="Aguardando garçom")); }

public record ItemComandaViewModel(ComandaSalaoViewModel Mesa,ItemComandaDTO Item);
public record ProdutoComandaViewModel(ComandaDetalhesDTO Comanda,ProdutoResponseDTO Produto);

public record RecebimentoComandaViewModel(int Indice,decimal Valor);
