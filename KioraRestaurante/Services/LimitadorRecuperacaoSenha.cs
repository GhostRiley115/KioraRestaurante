using System.Net;
using System.Threading.RateLimiting;

namespace KioraRestaurante.Services;

// Limites separados do login, compartilhados por conta entre as duas etapas de conferência.
// São locais à instância; múltiplas instâncias exigem armazenamento compartilhado antes de escalar.
public sealed class LimitadorRecuperacaoSenha(bool limitarPorIp = false) : IDisposable
{
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(10);
    private readonly PartitionedRateLimiter<string> _envios = Criar(3);
    private readonly PartitionedRateLimiter<string> _conferencias = Criar(10);
    private readonly PartitionedRateLimiter<string> _enviosIp = Criar(30);
    private readonly PartitionedRateLimiter<string> _conferenciasIp = Criar(60);

    private static PartitionedRateLimiter<string> Criar(int limite) =>
        PartitionedRateLimiter.Create<string, string>(chave =>
            RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite, Window = Janela, QueueLimit = 0, AutoReplenishment = true
            }));

    public RateLimitLease Adquirir(string emailCanonico, bool envio, IPAddress? ip)
    {
        if (limitarPorIp)
        {
            // Nunca usa diretamente X-Forwarded-For fornecido pelo cliente.
            if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
            var permissao = (envio ? _enviosIp : _conferenciasIp).AttemptAcquire(ip?.ToString() ?? "sem-ip");
            if (!permissao.IsAcquired) return permissao;
            permissao.Dispose();
        }
        return (envio ? _envios : _conferencias).AttemptAcquire(emailCanonico.Trim().ToLowerInvariant());
    }

    public void Dispose()
    {
        _envios.Dispose(); _conferencias.Dispose(); _enviosIp.Dispose(); _conferenciasIp.Dispose();
    }
}
