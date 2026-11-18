using System.Security.Claims;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KioraRestaurante.Services.Autenticacao;

namespace KioraRestaurante.Configuracao;

public static class AutenticacaoKioraExtensions
{
    public static IServiceCollection AddAutenticacaoKiora(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.Secao))
            .Validate(o => o.Valida, "Configure Jwt:ChaveSecreta com pelo menos 32 bytes aleatórios e uma validade entre 5 e 120 minutos.")
            .ValidateOnStart();
        // ======================================================
        // AUTENTICAÇÃO POR COOKIE E JWT
        // ======================================================

        services.AddAuthentication(options =>
        {
            // Seleciona automaticamente Cookie ou JWT.
            options.DefaultScheme = "CookieOuJwt";
            options.DefaultAuthenticateScheme = "CookieOuJwt";
            options.DefaultChallengeScheme = "CookieOuJwt";
            options.DefaultForbidScheme = "CookieOuJwt";

            // Login e logout do site continuam utilizando Cookies.
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddPolicyScheme("CookieOuJwt", "Cookie ou JWT", options =>
        {
            // Se houver Bearer no cabeçalho, utiliza JWT.
            // Caso contrário, utiliza Cookie.
            options.ForwardDefaultSelector = context =>
            {
                // Tokens do app só são aceitos nas APIs. O delivery e as Views mantêm o cookie do site.
                if (!context.Request.Path.StartsWithSegments("/api"))
                    return CookieAuthenticationDefaults.AuthenticationScheme;
                var authorization = context.Request.Headers.Authorization.ToString();

                return authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;
            };
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/Conta/Login";
            options.LogoutPath = "/Conta/Logout";
            options.AccessDeniedPath = "/Home/AcessoNegado";

            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;

            // Verifica se a conta continua ativa.
            options.EventsType = typeof(ValidarSessaoUsuario);
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((options, configuracao) =>
        {
            // Recupera a chave secreta armazenada no User Secrets.
            var jwt = configuracao.Value;
            var chaveSecreta = jwt.ChaveSecreta;

            if (string.IsNullOrWhiteSpace(chaveSecreta))
            {
                throw new InvalidOperationException(
                    "A chave secreta do JWT não foi configurada.");
            }

            // Define as regras de validação do JWT.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(chaveSecreta)),

                ValidateIssuer = true,
                ValidIssuer = jwt.Emissor,

                ValidateAudience = true,
                ValidAudience = jwt.Publico,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Verifica se o usuário e a sessão continuam válidos.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var id = context.Principal?
                        .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    var versao = context.Principal?
                        .FindFirst("VersaoSessao")?.Value;

                    // Verifica as informações obrigatórias do token.
                    if (!int.TryParse(id, out var usuarioId) ||
                        !Guid.TryParse(versao, out var versaoSessao) ||
                        versaoSessao == Guid.Empty)
                    {
                        context.Fail("Token inválido.");
                        return;
                    }

                    // Recupera o serviço de usuários.
                    var banco = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                    // Busca o usuário ativo.
                    var usuario = await banco.Usuarios.AsNoTracking()
                        .Where(u => u.Id == usuarioId && u.Ativo && !u.ConvitePendente)
                        .Select(u => new { u.VersaoSessao, u.Tipo }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                    var tipoToken = context.Principal?
                        .FindFirst(ClaimTypes.Role)?.Value;

                    // Confere conta, sessão e perfil.
                    if (usuario == null ||
                        usuario.VersaoSessao != versaoSessao ||
                        usuario.Tipo.ToString() != tipoToken)
                    {
                        context.Fail("Sessão inválida ou expirada.");
                    }
                }
            };
        });


        return services;
    }
}
