using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Filters;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Services.Autenticacao;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

// Gerencia os dispositivos autorizados a receber notificações Firebase do Kiora.
// Preserva a identificação do visitante, autenticação JWT e proteção antifalsificação.

[ApiController]
[Route("api/reservas/dispositivos")]
[ValidarAntifalsificacaoApi]
[EnableRateLimiting("reservas-escrita")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class DispositivosPushApiController(
    AppDbContext db,
    IdentidadeReserva identidade,
    IDataProtectionProvider protecao,
    TimeProvider relogio
) : ControllerBase
{
    // Mantém a finalidade de proteção original, preservando a compatibilidade com os tokens anteriormente armazenados.

    private readonly IDataProtector _protetor =
        protecao.CreateProtector(
            "Kiora.Push.FcmToken.v1");

    // REGISTRAR DISPOSITIVO

    // POST /api/reservas/dispositivos
    
    // Registra ou atualiza um dispositivo, respeitando sua associação anterior.
   
    [HttpPost]
    public async Task<IActionResult> Registrar(
        [FromBody] TokenDispositivoPushDTO dto,
        CancellationToken ct)
    {
        var token = dto.Token.Trim();

        if (!TokenValido(token))
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Token do dispositivo inválido."
            });
        }

        var acesso =
            identidade.Obter(HttpContext);

        if (!PossuiIdentidade(acesso))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Abra a sessão de reservas antes " +
                    "de registrar o dispositivo."
            });
        }

        var versaoSessao =
            ObterVersaoSessao(acesso);

        if (!await SessaoValida(
                acesso,
                versaoSessao,
                ct))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Sessão inválida ou expirada."
            });
        }

        var agora =
            relogio.GetUtcNow().UtcDateTime;

        var hash =
            CalcularHash(token);

        var dispositivo =
            await db.DispositivosPush
                .SingleOrDefaultAsync(
                    d => d.TokenHash == hash,
                    ct);

        if (dispositivo is null)
        {
            // Primeiro registro deste token.

            dispositivo = new DispositivoPush
            {
                TokenHash = hash,

                TokenFcm =
                    _protetor.Protect(token),

                UsuarioId =
                    acesso.UsuarioId,

                VisitanteHash =
                    acesso.VisitanteHash,

                VersaoSessaoUsuario =
                    versaoSessao,

                Ativo = true,

                CriadoEmUtc = agora,

                AtualizadoEmUtc = agora,

                DesativadoEmUtc = null,

                Versao = Guid.NewGuid()
            };

            db.DispositivosPush.Add(
                dispositivo);
        }
        else
        {
            // Não permite que qualquer pessoa que conheça o token FCM altere livremente seu proprietário.

            if (!await PodeRegistrar(
                    dispositivo,
                    acesso,
                    ct))
            {
                return Conflict(new
                {
                    sucesso = false,

                    mensagem =
                        "Não foi possível vincular " +
                        "o dispositivo à sessão atual.",

                    renovarToken = true
                });
            }

            var mesmoUsuario =
                acesso.UsuarioId.HasValue &&
                dispositivo.UsuarioId ==
                    acesso.UsuarioId;

            // Preserva a identificação anterior do visitante quando o mesmo usuário não apresenta uma nova chave.

            var visitanteHash =
                acesso.VisitanteHash;

            if (mesmoUsuario &&
                string.IsNullOrEmpty(
                    visitanteHash))
            {
                visitanteHash =
                    dispositivo.VisitanteHash;
            }

            dispositivo.TokenFcm =
                _protetor.Protect(token);

            dispositivo.UsuarioId =
                acesso.UsuarioId;

            dispositivo.VisitanteHash =
                visitanteHash;

            dispositivo.VersaoSessaoUsuario =
                versaoSessao;

            dispositivo.Ativo = true;

            dispositivo.AtualizadoEmUtc =
                agora;

            dispositivo.DesativadoEmUtc =
                null;

            dispositivo.Versao =
                Guid.NewGuid();
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "O dispositivo foi alterado " +
                    "simultaneamente. Tente novamente."
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível registrar " +
                    "o dispositivo. Tente novamente."
            });
        }

        // Nunca retorna o token FCM, seu hash ou sua forma criptografada.

        return Ok(new
        {
            sucesso = true
        });
    }

    // DESATIVAR DISPOSITIVO

    // POST /api/reservas/dispositivos/desativar
    // Desativa o dispositivo somente quando pertence à identidade autorizada.
    
    [HttpPost("desativar")]
    public async Task<IActionResult> Desativar(
        [FromBody] TokenDispositivoPushDTO dto,
        CancellationToken ct)
    {
        var token =
            dto.Token.Trim();

        if (!TokenValido(token))
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem =
                    "Token do dispositivo inválido."
            });
        }

        var acesso =
            identidade.Obter(HttpContext);

        if (!PossuiIdentidade(acesso))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível identificar " +
                    "a sessão."
            });
        }

        var versaoSessao =
            ObterVersaoSessao(acesso);

        if (!await SessaoValida(
                acesso,
                versaoSessao,
                ct))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Sessão inválida ou expirada."
            });
        }

        var hash =
            CalcularHash(token);

        var dispositivo =
            await db.DispositivosPush
                .SingleOrDefaultAsync(
                    d => d.TokenHash == hash,
                    ct);

        // Resposta idempotente.        
        // Não revela se o token existe ou pertence a outra pessoa.

        if (dispositivo is null ||
            !PertenceAoSolicitante(
                dispositivo,
                acesso) ||
            !dispositivo.Ativo)
        {
            return Ok(new
            {
                sucesso = true
            });
        }

        var agora =
            relogio.GetUtcNow().UtcDateTime;

        dispositivo.Ativo = false;

        dispositivo.DesativadoEmUtc =
            agora;

        dispositivo.AtualizadoEmUtc =
            agora;

        dispositivo.Versao =
            Guid.NewGuid();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "O dispositivo mudou durante " +
                    "a operação. Tente novamente."
            });
        }

        return Ok(new
        {
            sucesso = true
        });
    }

    // REGRAS DE ASSOCIAÇÃO DO DISPOSITIVO
    private async Task<bool> PodeRegistrar(
        DispositivoPush dispositivo,
        AcessoReserva acesso,
        CancellationToken ct)
    {
        // O mesmo usuário autenticado pode atualizar seu próprio dispositivo.

        if (acesso.UsuarioId.HasValue &&
            dispositivo.UsuarioId ==
                acesso.UsuarioId)
        {
            return true;
        }

        var mesmoVisitante =
            MesmoVisitante(
                dispositivo,
                acesso);

        // Uma identidade diferente não pode assumir o dispositivo sem comprovar a identidade original do visitante.

        if (!mesmoVisitante)
        {
            return false;
        }

        // Permite vincular um visitante à sua conta depois do login.

        if (!dispositivo.UsuarioId.HasValue)
        {
            return true;
        }

        // Permite trocar a associação quando o dispositivo foi previamente desativado e a chave do visitante permanece válida.

        if (!dispositivo.Ativo)
        {
            return true;
        }

        // Registros anteriores à migration não possuem versão de sessão.
        // A reassociação exige a chave original do visitante.

        if (!dispositivo
                .VersaoSessaoUsuario.HasValue)
        {
            return true;
        }

        var usuarioAnteriorId =
            dispositivo.UsuarioId.Value;

        var versaoAnterior =
            dispositivo
                .VersaoSessaoUsuario.Value;

        // Verifica se a sessão anterior ainda continua válida.

        var sessaoAnteriorValida =
            await db.Usuarios
                .AsNoTracking()
                .AnyAsync(
                    u =>
                        u.Id ==
                            usuarioAnteriorId &&
                        u.Ativo &&
                        !u.ConvitePendente &&
                        u.VersaoSessao ==
                            versaoAnterior,
                    ct);

        // Se a sessão anterior foi revogada, permite nova associação à mesma identidade de visitante.

        return !sessaoAnteriorValida;
    }

    // VERIFICAR PROPRIETÁRIO
    private static bool PertenceAoSolicitante(
        DispositivoPush dispositivo,
        AcessoReserva acesso)
    {
        // Dispositivos vinculados a uma conta somente podem ser desativados pelo próprio usuário autenticado.

        if (dispositivo.UsuarioId.HasValue)
        {
            return acesso.UsuarioId.HasValue &&
                dispositivo.UsuarioId ==
                    acesso.UsuarioId;
        }

        // Dispositivos de visitantes exigem a chave de visitante correspondente.

        return MesmoVisitante(
            dispositivo,
            acesso);
    }
    private static bool MesmoVisitante(
        DispositivoPush dispositivo,
        AcessoReserva acesso)
    {
        return !string.IsNullOrEmpty(
                    acesso.VisitanteHash) &&
            dispositivo.VisitanteHash ==
                acesso.VisitanteHash;
    }

    // IDENTIFICAR SESSÃO AUTENTICADA
    private Guid? ObterVersaoSessao(
        AcessoReserva acesso)
    {
        if (!acesso.UsuarioId.HasValue)
        {
            return null;
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // JWT utilizado pelo Mobile.

        var valor =
            User.FindFirstValue(
                "VersaoSessao");

        // Compatibilidade com a autenticação por cookie utilizada pelo site.

        valor ??=
            User.FindFirstValue(
                ValidarSessaoUsuario
                    .ClaimVersaoSessao);

        if (!Guid.TryParse(
                valor,
                out var versao) ||
            versao == Guid.Empty)
        {
            return null;
        }

        return versao;
    }

    // VALIDAR A CONTA E A VERSÃO DA SESSÃO
    private async Task<bool> SessaoValida(
        AcessoReserva acesso,
        Guid? versaoSessao,
        CancellationToken ct)
    {
        // Visitantes não possuem versão de sessão autenticada.

        if (!acesso.UsuarioId.HasValue)
        {
            return !string.IsNullOrEmpty(
                acesso.VisitanteHash);
        }

        if (!versaoSessao.HasValue ||
            versaoSessao.Value ==
                Guid.Empty)
        {
            return false;
        }

        var usuarioId =
            acesso.UsuarioId.Value;

        var versao =
            versaoSessao.Value;

        return await db.Usuarios
            .AsNoTracking()
            .AnyAsync(
                u => u.Id == usuarioId &&
                    u.Ativo &&
                    !u.ConvitePendente &&
                    u.VersaoSessao == versao,
                ct);
    }

    // VALIDAR IDENTIDADE
    private static bool PossuiIdentidade(
        AcessoReserva acesso)
    {
        return acesso.UsuarioId.HasValue ||
            !string.IsNullOrEmpty(
                acesso.VisitanteHash);
    }

    // VALIDAR TOKEN FCM
    private static bool TokenValido(
        string token)
    {
        // Mantém os limites e caracteres aceitos pela implementação original.

        return token.Length is >= 20 and <= 1024 &&
            token.All(c =>
                c is >= '!' and <= '~');
    }

    // CALCULAR HASH SHA-256    
    private static string CalcularHash(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }
}

// CONTRATO JSON DO MOBILE
public sealed class TokenDispositivoPushDTO
{
    [Required]
    [StringLength(
        1024,
        MinimumLength = 20)]
    public string Token { get; set; } =
        string.Empty;
}
