using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Filters;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

/// <summary>
/// Registra e desativa dispositivos autorizados
/// a receber notificações Firebase do Kiora.
/// </summary>
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
    IDataProtectionProvider protecao
) : ControllerBase
{
    // Protege os tokens FCM armazenados no banco.
    // A mesma finalidade será usada pelo serviço de envio.
    private readonly IDataProtector _protetor =
        protecao.CreateProtector(
            "Kiora.Push.FcmToken.v1"
        );

    /// <summary>
    /// POST /api/reservas/dispositivos
    ///
    /// Associa o token FCM ao usuário autenticado
    /// e/ou à identificação do visitante.
    /// </summary>
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
                mensagem = "Token do dispositivo inválido."
            });
        }

        // Reutiliza a identificação oficial da API.
        var acesso = identidade.Obter(HttpContext);

        if (!PossuiIdentidade(acesso))
        {
            return Unauthorized(new
            {
                mensagem =
                    "Abra a sessão de reservas antes " +
                    "de registrar o dispositivo."
            });
        }

        var agora = DateTime.UtcNow;
        var hash = CalcularHash(token);

        // Cada token possui apenas um registro.
        var dispositivo = await db.DispositivosPush
            .SingleOrDefaultAsync(
                d => d.TokenHash == hash,
                ct
            );

        if (dispositivo is null)
        {
            dispositivo = new DispositivoPush
            {
                TokenHash = hash,
                TokenFcm = _protetor.Protect(token),

                UsuarioId = acesso.UsuarioId,
                VisitanteHash = acesso.VisitanteHash,

                Ativo = true,

                CriadoEmUtc = agora,
                AtualizadoEmUtc = agora,
                DesativadoEmUtc = null
            };

            db.DispositivosPush.Add(dispositivo);
        }
        else
        {
            // Atualiza a associação quando o
            // aplicativo muda de usuário ou sessão.
            dispositivo.TokenFcm =
                _protetor.Protect(token);

            dispositivo.UsuarioId = acesso.UsuarioId;
            dispositivo.VisitanteHash =
                acesso.VisitanteHash;

            dispositivo.Ativo = true;
            dispositivo.AtualizadoEmUtc = agora;
            dispositivo.DesativadoEmUtc = null;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensagem =
                    "Não foi possível registrar o dispositivo. " +
                    "Tente novamente."
            });
        }

        // Nunca retorna o token ao aplicativo.
        return Ok(new
        {
            sucesso = true
        });
    }

    /// <summary>
    /// POST /api/reservas/dispositivos/desativar
    ///
    /// Desativa as notificações somente se o
    /// dispositivo pertencer ao solicitante.
    /// </summary>
    [HttpPost("desativar")]
    public async Task<IActionResult> Desativar(
        [FromBody] TokenDispositivoPushDTO dto,
        CancellationToken ct)
    {
        var token = dto.Token.Trim();

        if (!TokenValido(token))
        {
            return BadRequest(new
            {
                mensagem = "Token do dispositivo inválido."
            });
        }

        var acesso = identidade.Obter(HttpContext);

        if (!PossuiIdentidade(acesso))
        {
            return Unauthorized(new
            {
                mensagem =
                    "Não foi possível identificar a sessão."
            });
        }

        var hash = CalcularHash(token);

        var dispositivo = await db.DispositivosPush
            .SingleOrDefaultAsync(
                d => d.TokenHash == hash,
                ct
            );

        if (dispositivo is null)
        {
            // Operação idempotente:
            // um dispositivo já ausente está desativado.
            return Ok(new
            {
                sucesso = true
            });
        }

        // Impede desativar um dispositivo pertencente
        // exclusivamente a outra identidade.
        if (!PertenceAoSolicitante(dispositivo, acesso))
        {
            return Ok(new
            {
                sucesso = true
            });
        }

        dispositivo.Ativo = false;
        dispositivo.DesativadoEmUtc = DateTime.UtcNow;
        dispositivo.AtualizadoEmUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            sucesso = true
        });
    }

    /// <summary>
    /// Exige uma conta autenticada ou
    /// uma identificação válida de visitante.
    /// </summary>
    private static bool PossuiIdentidade(
        AcessoReserva acesso)
    {
        return acesso.UsuarioId.HasValue ||
            !string.IsNullOrEmpty(acesso.VisitanteHash);
    }

    /// <summary>
    /// Valida os caracteres e o tamanho do token.
    /// O token FCM é tratado como dado opaco.
    /// </summary>
    private static bool TokenValido(string token)
    {
        return token.Length is >= 20 and <= 1024 &&
            token.All(c => c is >= '!' and <= '~');
    }

    /// <summary>
    /// Verifica se o registro pertence ao usuário
    /// ou à identidade de visitante apresentada.
    /// </summary>
    private static bool PertenceAoSolicitante(
        DispositivoPush dispositivo,
        AcessoReserva acesso)
    {
        var mesmoUsuario =
            acesso.UsuarioId.HasValue &&
            dispositivo.UsuarioId ==
                acesso.UsuarioId;

        var mesmoVisitante =
            !string.IsNullOrEmpty(
                acesso.VisitanteHash
            ) &&
            dispositivo.VisitanteHash ==
                acesso.VisitanteHash;

        return mesmoUsuario || mesmoVisitante;
    }

    /// <summary>
    /// Calcula o hash utilizado para localizar
    /// o token sem pesquisar seu valor original.
    /// </summary>
    private static string CalcularHash(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)
            )
        );
    }
}

/// <summary>
/// Dados recebidos do aplicativo Android.
/// </summary>
public sealed class TokenDispositivoPushDTO
{
    [Required]
    [StringLength(
        1024,
        MinimumLength = 20)]
    public string Token { get; set; } =
        string.Empty;
}