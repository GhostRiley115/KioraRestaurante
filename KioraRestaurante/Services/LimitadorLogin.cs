using System.Net;
using System.Threading.RateLimiting;

namespace KioraRestaurante.Services;

// Mantém os limites compartilhados entre as requisições desta instância da aplicação.
// As partições inativas são removidas pelo limitador do próprio .NET.
public sealed class LimitadorLogin(bool limitarPorIp = false) : IDisposable
{
    public const int TamanhoMaximoEmail = 150;
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<string> _porIp = CriarLimitador(20);
    private readonly PartitionedRateLimiter<string> _porEmail = CriarLimitador(5);

    private static PartitionedRateLimiter<string> CriarLimitador(int limite)
    {
        return PartitionedRateLimiter.Create<string, string>(chave =>
            string.IsNullOrEmpty(chave)
                ? RateLimitPartition.GetNoLimiter(string.Empty)
                : RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = Janela,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public RateLimitLease AdquirirPermissao(IPAddress? enderecoIp, string? email)
    {
        // Usa apenas o endereço informado pela conexão; não confia em cabeçalhos enviados pelo cliente.
        // Unifica a representação IPv4 e a mesma origem apresentada como IPv6 mapeado.
        if (enderecoIp?.IsIPv4MappedToIPv6 == true)
            enderecoIp = enderecoIp.MapToIPv4();

        // Atrás de um proxy, habilite este limite somente após configurar a origem confiável.
        // O padrão por e-mail evita compartilhar o bloqueio entre todos os usuários do proxy.
        if (limitarPorIp)
        {
            var permissaoIp = _porIp.AttemptAcquire(enderecoIp?.ToString() ?? "sem-ip");
            if (!permissaoIp.IsAcquired)
                return permissaoIp;

            // Descartar a permissão não devolve tentativas de uma janela de tempo.
            permissaoIp.Dispose();
        }

        var emailSemEspacos = email.AsSpan().Trim();

        // Dados ausentes ou grandes demais consomem o limite por IP, quando habilitado,
        // mas não criam uma partição por e-mail inválido nem um bloqueio compartilhado.
        if (emailSemEspacos.IsEmpty || emailSemEspacos.Length > TamanhoMaximoEmail)
            return _porEmail.AttemptAcquire(string.Empty);

        return _porEmail.AttemptAcquire(emailSemEspacos.ToString().ToLowerInvariant());
    }

    public void Dispose()
    {
        _porIp.Dispose();
        _porEmail.Dispose();
    }
}
