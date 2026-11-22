using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Filters;
using KioraRestaurante.Services.Email;
using KioraRestaurante.Services.Usuarios;
using KioraRestaurante.ViewModels.Conta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers.Api;

/// <summary>
/// Disponibiliza a recuperação de senha
/// para o aplicativo Kiora Mobile.
///
/// Reutiliza os serviços, validações e
/// limites de tentativas do site.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/usuarios/recuperacao")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class RecuperacaoSenhaApiController
    : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly IEmailService _emailService;

    public RecuperacaoSenhaApiController(
        IUsuarioService usuarioService,
        IEmailService emailService)
    {
        _usuarioService = usuarioService;
        _emailService = emailService;
    }

    // ==================================================
    // SOLICITAR CÓDIGO DE RECUPERAÇÃO
    // ==================================================

    /// <summary>
    /// Solicita o envio de um código de
    /// recuperação ao e-mail cadastrado.
    /// </summary>
    [HttpPost("solicitar")]
    [ServiceFilter(typeof(LimitarRecuperacaoSenha))]
    public async Task<IActionResult> Solicitar(
        [FromBody] EsqueciSenhaViewModel model)
    {
        var dto = new UsuarioSolicitarRecuperacaoDTO
        {
            Email = model.Email
        };

        // Gera um código somente para uma
        // conta ativa e apta à recuperação.
        var recuperacao =
            _usuarioService.GerarTokenRecuperacao(dto);

        if (recuperacao != null)
        {
            // Utiliza o endereço efetivamente
            // cadastrado no banco de dados.
            var enviado = await _emailService
                .EnviarCodigoRecuperacaoSenhaAsync(
                    recuperacao.Email,
                    recuperacao.Codigo);

            // Invalida somente o código cujo
            // envio não pôde ser concluído.
            if (!enviado)
            {
                _usuarioService.InvalidarCodigoRecuperacao(
                    recuperacao);
            }
        }

        // A resposta é igual para contas
        // existentes, inexistentes ou inativas.
        return Ok(new
        {
            sucesso = true,

            mensagem =
                "Se o e-mail estiver cadastrado, " +
                "você receberá um código de recuperação."
        });
    }

    // ==================================================
    // VALIDAR CÓDIGO DE RECUPERAÇÃO
    // ==================================================

    /// <summary>
    /// Verifica se o código pertence à conta
    /// informada e permanece dentro da validade.
    /// </summary>
    [HttpPost("validar")]
    [ServiceFilter(typeof(LimitarRecuperacaoSenha))]
    public IActionResult Validar(
        [FromBody] ValidarCodigoRecuperacaoViewModel model)
    {
        var valido =
            _usuarioService.ValidarCodigoRecuperacao(
                model.Email,
                model.Codigo);

        if (!valido)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Código inválido ou expirado."
            });
        }

        return Ok(new
        {
            sucesso = true,

            mensagem =
                "Código validado com sucesso."
        });
    }

    // ==================================================
    // REDEFINIR SENHA
    // ==================================================

    /// <summary>
    /// Redefine a senha após conferir novamente
    /// o código e sua validade.
    ///
    /// O serviço invalida o código utilizado
    /// e revoga as sessões anteriores.
    /// </summary>
    [HttpPost("redefinir")]
    [ServiceFilter(typeof(LimitarRecuperacaoSenha))]
    public IActionResult Redefinir(
        [FromBody] RedefinirSenhaViewModel model)
    {
        var dto = new UsuarioRedefinirSenhaDTO
        {
            Email = model.Email,

            Token = model.Codigo,

            NovaSenha = model.NovaSenha,

            ConfirmarNovaSenha =
                model.ConfirmarNovaSenha
        };

        var redefinida =
            _usuarioService.RedefinirSenha(dto);

        if (!redefinida)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Código inválido ou expirado."
            });
        }

        return Ok(new
        {
            sucesso = true,

            mensagem =
                "Senha redefinida com sucesso."
        });
    }
}
