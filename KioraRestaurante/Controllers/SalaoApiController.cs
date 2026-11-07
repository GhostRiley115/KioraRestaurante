using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.Services.Reservas;
using KioraRestaurante.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

[ApiController,Route("api/salao"),Authorize,AutoValidateAntiforgeryToken,EnableRateLimiting("salao")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class SalaoApiController(ConsultaSalaoService consulta,ComandaService comandas,PortariaService portaria,CozinhaService cozinha,
    AcessoSalaoService acesso,IProdutoService produtos):ControllerBase
{
    private int Id=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<IActionResult> Executar(Func<Task<object?>> acao)
    {
        try{return Ok(await acao());}
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(Exception ex) when(ex is ValidationException or RegraReservaException){return Conflict(new{mensagem=ex.Message});}
        catch(DbUpdateException){return Conflict(new{mensagem="Os dados mudaram ou não puderam ser gravados. Atualize o estado; ao repetir a mesma solicitação, preserve sua chave."});}
    }
    private Task<IActionResult> Alterar(Func<Task> acao)=>Executar(async()=>{await acao();return new{sucesso=true};});
    [HttpGet("mesas")] public Task<IActionResult> Mesas(CancellationToken ct)=>Executar(async()=>await consulta.Mesas(Id,ct));
    [HttpGet("garcons")] public Task<IActionResult> Garcons(CancellationToken ct)=>Executar(async()=>await consulta.Garcons(Id,ct));
    [HttpGet("grupos")] public Task<IActionResult> Grupos(DateOnly? dia,CancellationToken ct)=>Executar(async()=>await portaria.Grupos(Id,dia,ct));
    [HttpGet("cardapio")] public Task<IActionResult> Cardapio()=>Executar(async()=>{await acesso.Exigir(Id,default,TipoUsuario.Garcom,TipoUsuario.Gerente);return await produtos.ListarCardapio(null);});
    [HttpGet("comandas/{id:guid}")] public Task<IActionResult> Comanda(Guid id,CancellationToken ct)=>Executar(async()=>await consulta.Comanda(Id,id,ct));
    [HttpPost("acomodar")] public Task<IActionResult> Acomodar(AcomodarMesaDTO dto,CancellationToken ct)=>Executar(async()=>new{id=await comandas.Acomodar(Id,dto,ct)});
    [HttpPost("rodadas")] public Task<IActionResult> Adicionar(AdicionarRodadaDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Adicionar(Id,dto,ct));
    [HttpPost("itens/corrigir")] public Task<IActionResult> Corrigir(CorrigirItemDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Corrigir(Id,dto,ct));
    [HttpPost("itens/entregar")] public Task<IActionResult> Entregar(EntregarItemDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Entregar(Id,dto,ct));
    [HttpPost("comandas/transferir")] public Task<IActionResult> Transferir(TransferirMesaDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Transferir(Id,dto,ct));
    [HttpPost("comandas/desconto")] public Task<IActionResult> Desconto(DescontoComandaDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Descontar(Id,dto,ct));
    [HttpPost("comandas/fechar")] public Task<IActionResult> Fechar(FecharComandaDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Fechar(Id,dto,ct));
    [HttpPost("comandas/liberar")] public Task<IActionResult> Liberar(ComandaComandoDTO dto,CancellationToken ct)=>Alterar(()=>comandas.Liberar(Id,dto,ct));
    [HttpPost("fila")] public Task<IActionResult> Fila(FilaPresencialDTO dto,CancellationToken ct)=>Executar(async()=>new{codigo=await portaria.Fila(Id,dto,ct)});
    [HttpPost("chegada")] public Task<IActionResult> Chegada(ChegadaReservaDTO dto,CancellationToken ct)=>Alterar(()=>portaria.Chegada(Id,dto,ct));
    [HttpPost("fila/chamar")] public Task<IActionResult> Chamar(ChamarGrupoDTO dto,CancellationToken ct)=>Alterar(()=>portaria.Chamar(Id,dto.MesaId,dto.ChaveSolicitacao,ct));
    [HttpPost("reservas/cancelar")] public Task<IActionResult> Cancelar(CancelarGrupoDTO dto,CancellationToken ct)=>Alterar(()=>portaria.Cancelar(Id,new OperarReservaDTO{Codigo=dto.Codigo,Versao=dto.Versao,Acao="cancelar",Motivo=dto.Motivo},dto.ChaveSolicitacao,ct));
    [HttpPost("mesas/configurar")] public Task<IActionResult> Configurar(ConfigurarMesaDTO dto,CancellationToken ct)=>Alterar(()=>portaria.ConfigurarMesa(Id,dto,ct));
    [HttpGet("cozinha")] public Task<IActionResult> Cozinha(CancellationToken ct)=>Executar(async()=>await cozinha.Fila(Id,ct));
    [HttpPost("cozinha/preparo")] public Task<IActionResult> Preparo(PreparoItemDTO dto,CancellationToken ct)=>Alterar(()=>cozinha.Atualizar(Id,dto,ct));
    [HttpGet("relatorio")] public Task<IActionResult> Relatorio(DateOnly dia,DateOnly comparar,CancellationToken ct)=>Executar(async()=>await consulta.Relatorio(Id,dia,comparar,ct));
}
