using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Services.Salao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Controllers;

[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class FuncionariosController(FuncionarioService service) : Controller
{
    private int Id=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    [Authorize(Roles="Administrador,Gerente"),HttpGet] public async Task<IActionResult> Index()
    {try{return View(await service.Listar(Id));}catch(UnauthorizedAccessException){return Forbid();}}
    [Authorize(Roles="Administrador,Gerente"),HttpGet] public async Task<IActionResult> Editar(int? id)
    {try{return View(id.HasValue?await service.Edicao(Id,id.Value):new SalvarFuncionarioDTO());}catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}}
    [Authorize(Roles="Administrador,Gerente"),HttpPost,EnableRateLimiting("salao")] public async Task<IActionResult> Editar(SalvarFuncionarioDTO dto)
    {
        if(ModelState.IsValid)try
        {
            var resultado=await service.Salvar(Id,dto);
            TempData["EquipeSucesso"]=resultado.ConviteEnviado?"Cadastro salvo. Quando necessário, o convite para definir a senha foi enviado por e-mail.":"Cadastro salvo, mas o convite não pôde ser enviado. Confira a configuração de e-mail e use Reenviar convite.";
            return RedirectToAction(nameof(Index));
        }
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}
        catch(DbUpdateException){ModelState.AddModelError("","O cadastro mudou ou o e-mail já está em uso. Abra a edição novamente.");}
        return View(dto);
    }
    [Authorize(Roles="Administrador,Gerente"),HttpPost,EnableRateLimiting("convite-funcionario")] public async Task<IActionResult> Reenviar(int funcionarioId,Guid versao)
    {
        try{var enviado=await service.Reenviar(Id,funcionarioId,versao);TempData[enviado?"EquipeSucesso":"EquipeErro"]=enviado?"Novo convite enviado. O link anterior foi invalidado.":"Não foi possível enviar o convite. Confira a configuração de e-mail.";}
        catch(UnauthorizedAccessException){return Forbid();}catch(KeyNotFoundException){return NotFound();}
        catch(ValidationException ex){TempData["EquipeErro"]=ex.Message;}
        catch(DbUpdateException){TempData["EquipeErro"]="O cadastro mudou. Confira a situação atual antes de reenviar.";}
        return RedirectToAction(nameof(Index));
    }
    [AllowAnonymous,HttpGet,EnableRateLimiting("convite-funcionario")] public async Task<IActionResult> Convite(string? token)
    {
        Response.Headers["Referrer-Policy"]="no-referrer";
        if(!await service.ConferirConvite(token))ModelState.AddModelError("","Convite inválido ou expirado. Peça outro ao gerente.");
        return View(new AceitarConviteDTO{Token=token??""});
    }
    [AllowAnonymous,HttpPost,EnableRateLimiting("convite-funcionario")] public async Task<IActionResult> Convite(AceitarConviteDTO dto)
    {
        Response.Headers["Referrer-Policy"]="no-referrer";
        if(ModelState.IsValid)try{await service.Aceitar(dto);return View("ConviteConcluido");}
        catch(ValidationException ex){ModelState.AddModelError("",ex.Message);}
        catch(DbUpdateException){ModelState.AddModelError("","Não foi possível confirmar. Peça um novo convite ao gerente.");}
        ModelState.Remove(nameof(dto.Senha));ModelState.Remove(nameof(dto.ConfirmarSenha));dto.Senha="";dto.ConfirmarSenha="";return View(dto);
    }
}
