using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Models.Notificacoes;

namespace KioraRestaurante.DTOs.Notificacoes;

public class CriarNotificacaoDTO
{
    public Guid ChavePublicacao { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage = "Informe um título."), StringLength(120, ErrorMessage = "O título deve ter até 120 caracteres.")] public string Titulo { get; set; } = "";
    [Required(ErrorMessage = "Escreva a mensagem."), StringLength(1500, ErrorMessage = "A mensagem deve ter até 1500 caracteres.")] public string Mensagem { get; set; } = "";
    [EnumDataType(typeof(PublicoNotificacao), ErrorMessage = "Selecione um público válido.")] public PublicoNotificacao Publico { get; set; } = PublicoNotificacao.Cliente;
    public int? CupomId { get; set; }
    public string? TokenPrevia { get; set; }
}

public record PreviaNotificacao(CriarNotificacaoDTO Dados, int Destinatarios, string? CodigoCupom, DateTime ExpiraEmUtc);
public record ItemNotificacaoDTO(Guid Id, string Titulo, string Mensagem, DateTime CriadaEmUtc, bool NaoLida, bool TemDestino);
public record ListaNotificacoesDTO(List<ItemNotificacaoDTO> Itens, int Pagina, int TotalPaginas, int NaoLidas);
public record DestinoNotificacao(string Controller, string Action, int? Id = null, Guid? Codigo = null, string? Data = null);
public record ComunicadoAdminDTO(Guid Id, string Titulo, DateTime CriadaEmUtc, int Destinatarios, int Leituras, string? CodigoCupom);
