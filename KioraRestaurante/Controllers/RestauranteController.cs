using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Salao;
using KioraRestaurante.Services.Reservas;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

[Authorize(Roles="Administrador,Gerente,Garcom,Portaria,Cozinha"),AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class RestauranteController(ConsultaSalaoService consulta,ComandaService comandas,PortariaService portaria,CozinhaService cozinha,
    AcessoSalaoService acesso,IProdutoService produtos,HorariosReserva horarios,KioraRestaurante.Services.Notificacoes.NotificacaoService notificacoes):Controller
{
    private int Id=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<IActionResult> Consultar(Func<Task<IActionResult>> acao)
    {
        if(!ModelState.IsValid)return BadRequest();
        try{return await acao();}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(Exception ex) when(ex is ValidationException or RegraReservaException){TempData["EquipeErro"]=ex.Message;return RedirectToAction(nameof(Index));}
    }
    private async Task<IActionResult> Alterar(Func<Task> acao,Guid? id=null,string destino="Index",DateOnly? data=null)
    {
        if(!ModelState.IsValid)TempData["EquipeErro"]="Confira os campos preenchidos e atualize a tela antes de tentar novamente.";
        else try{await acao();TempData["EquipeSucesso"]="Operação registrada.";}
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(Exception ex) when(ex is ValidationException or RegraReservaException){TempData["EquipeErro"]=ex.Message;}
        catch(DbUpdateException){TempData["EquipeErro"]="Os dados mudaram ou não puderam ser salvos. Confira a situação atual antes de repetir.";}
        return RedirectToAction(destino,new{id,data=data?.ToString("yyyy-MM-dd")});
    }
    [HttpGet] public Task<IActionResult> Index()=>Consultar(async()=>
    {
        var u=await acesso.Exigir(Id,default,TipoUsuario.Garcom,TipoUsuario.Gerente,TipoUsuario.Portaria,TipoUsuario.Cozinha);
        if(u.Tipo==TipoUsuario.Cozinha)return RedirectToAction(nameof(Cozinha));
        if(u.Tipo==TipoUsuario.Portaria)return RedirectToAction(nameof(Portaria));
        return View(new PainelSalaoViewModel(await consulta.Mesas(Id),await portaria.Grupos(Id),await consulta.Garcons(Id)));
    });
    [HttpGet] public Task<IActionResult> Portaria(DateOnly? data)=>Consultar(async()=>
        View(new PainelSalaoViewModel(await consulta.Mesas(Id),await portaria.Grupos(Id,data),await consulta.Garcons(Id)){DataConsulta=data??horarios.Hoje}));
    [HttpGet] public Task<IActionResult> Comanda(Guid id)=>Consultar(async()=>View(new ComandaSalaoViewModel(await consulta.Comanda(Id,id),await produtos.ListarCardapio(null),await consulta.Garcons(Id))));
    [HttpGet] public Task<IActionResult> Cozinha()=>Consultar(async()=>{
        var fila=await cozinha.Fila(Id);
        ViewData["Correcoes"]=(await notificacoes.Listar(Id,1)).Itens.Where(n=>n.Titulo.Contains("correção no pedido",StringComparison.Ordinal)).Take(5).ToList();
        return View(fila);
    });
    [HttpGet] public Task<IActionResult> Relatorio(DateOnly? dia,DateOnly? comparar)=>Consultar(async()=>View(await consulta.Relatorio(Id,dia??horarios.Hoje,comparar??horarios.Hoje.AddDays(-1))));
    [HttpGet] public Task<IActionResult> Configurar(int? id)=>Consultar(async()=>
    {
        await acesso.Exigir(Id,default,TipoUsuario.Gerente);
        var mesas=await consulta.Mesas(Id);
        var mesa=id.HasValue?mesas.SingleOrDefault(m=>m.Id==id):null;
        if(id.HasValue&&mesa==null)return NotFound();
        return View(new ConfigurarMesasViewModel(new ConfigurarMesaDTO{ChaveSolicitacao=Guid.NewGuid(),MesaId=mesa?.Id??0,Versao=mesa?.Versao??Guid.Empty,Nome=mesa?.Nome??"",Capacidade=mesa?.Capacidade??2,Ativa=mesa?.Habilitada??true},mesas));
    });
    [HttpPost,EnableRateLimiting("salao")] public async Task<IActionResult> Acomodar(AcomodarMesaDTO dto)
    {
        Guid? id=null;var result=await Alterar(async()=>{id=await comandas.Acomodar(Id,dto);});
        return id.HasValue?RedirectToAction(nameof(Comanda),new{id}):result;
    }
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Adicionar(AdicionarRodadaDTO dto)=>Alterar(()=>comandas.Adicionar(Id,dto),dto.AtendimentoId,nameof(Comanda));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Corrigir(CorrigirItemDTO dto)=>Alterar(()=>comandas.Corrigir(Id,dto),dto.AtendimentoId,nameof(Comanda));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Entregar(EntregarItemDTO dto)=>Alterar(()=>comandas.Entregar(Id,dto),dto.AtendimentoId,nameof(Comanda));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Transferir(TransferirMesaDTO dto)=>Alterar(()=>comandas.Transferir(Id,dto),dto.AtendimentoId,nameof(Comanda));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Desconto(DescontoComandaDTO dto)=>Alterar(()=>comandas.Descontar(Id,dto),dto.AtendimentoId,nameof(Comanda));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Fechar(FecharComandaDTO dto)
    {
        dto.Pagamentos=(dto.Pagamentos??[]).Where(p=>p.Valor!=0).ToList();
        return Alterar(()=>comandas.Fechar(Id,dto),dto.AtendimentoId,nameof(Comanda));
    }
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Liberar(ComandaComandoDTO dto)=>Alterar(()=>comandas.Liberar(Id,dto));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Chegada(ChegadaReservaDTO dto,DateOnly? data)=>Alterar(()=>portaria.Chegada(Id,dto),destino:nameof(Portaria),data:data);
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Fila(FilaPresencialDTO dto)=>Alterar(async()=>{await portaria.Fila(Id,dto);},destino:nameof(Portaria));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Chamar(ChamarGrupoDTO dto)=>Alterar(()=>portaria.Chamar(Id,dto.MesaId,dto.ChaveSolicitacao),destino:nameof(Portaria));
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> CancelarReserva(CancelarGrupoDTO dto,DateOnly? data)=>Alterar(()=>portaria.Cancelar(Id,new OperarReservaDTO{Codigo=dto.Codigo,Versao=dto.Versao,Acao="cancelar",Motivo=dto.Motivo},dto.ChaveSolicitacao),destino:nameof(Portaria),data:data);
    [HttpPost,EnableRateLimiting("salao")] public Task<IActionResult> Configurar(ConfigurarMesaDTO dto)=>Alterar(()=>portaria.ConfigurarMesa(Id,dto),destino:nameof(Configurar));
    [HttpPost,EnableRateLimiting("salao")] public async Task<IActionResult> Preparo(PreparoItemDTO dto,string? origemFiltro,string? etapaFiltro)
    {
        var resultado=await Alterar(()=>cozinha.Atualizar(Id,dto),destino:nameof(Cozinha));
        return resultado is RedirectToActionResult?RedirectToAction(nameof(Cozinha),new{origem=origemFiltro is "salao" or "delivery"?origemFiltro:"todas",etapa=etapaFiltro is "NaFila" or "EmPreparo" or "Pronto"?etapaFiltro:"todas"}):resultado;
    }
}
