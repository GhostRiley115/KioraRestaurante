using System.Security.Cryptography;
using System.Text;
namespace KioraRestaurante.Services.PagamentosOnline;
public static class AssinaturaOrders
{
    public static bool Validar(string assinatura, string requestId, string dataId, string segredo)
    {
        if (string.IsNullOrWhiteSpace(segredo) || string.IsNullOrWhiteSpace(requestId)
                                               || requestId.Length > 200 || !MpOrdersClient.IdValido(dataId)
                                               || string.IsNullOrWhiteSpace(assinatura) || assinatura.Length > 500) return false;
        var partes = assinatura.Split(',').Select(x => x.Trim().Split('=', 2)).ToArray();
        var ts = partes.Where(p => p.Length == 2 && p[0] == "ts").ToArray();
        var v1 = partes.Where(p => p.Length == 2 && p[0] == "v1").ToArray();
        if (ts.Length != 1 || v1.Length != 1 || !long.TryParse(ts[0][1], out var instante) || instante <= 0)
            return false;
        var manifesto = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts[0][1]};";
        var esperado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(manifesto));
        try { return CryptographicOperations.FixedTimeEquals(esperado, Convert.FromHexString(v1[0][1])); }
        catch (FormatException) { return false; }
        // Reenvios são aceitos: a aplicação consulta o estado atual por ID e data.
    }
}