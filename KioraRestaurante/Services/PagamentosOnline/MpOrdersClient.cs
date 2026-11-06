using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KioraRestaurante.Options;
using Microsoft.Extensions.Options;
namespace KioraRestaurante.Services.PagamentosOnline;
public class FalhaPagamento(string mensagem) : Exception(mensagem);
public class MpOrdersClient(HttpClient http, IOptions<MercadoPagoConfig> options)
{
    public static bool IdValido(string id) => id.Length <= 80
        && Regex.IsMatch(id, @"\A[A-Za-z0-9]+\z");
    public static bool UrlValida(string? texto) => Uri.TryCreate(texto, UriKind.Absolute, out var u)
        && u.Scheme == "https" && u.IsDefaultPort && u.UserInfo == ""
        && u.Host is "www.mercadopago.com.br" or "mercadopago.com.br";
    public async Task<JsonDocument> Enviar(string? id, string? corpo, string? chave, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Ativado || string.IsNullOrWhiteSpace(o.AccessToken))
            throw new FalhaPagamento("O pagamento online não está habilitado.");
        if (id != null && !IdValido(id)) throw new FalhaPagamento("Identificador externo inválido.");
        using var req = new HttpRequestMessage(id == null ? HttpMethod.Post : HttpMethod.Get,
            id == null ? "v1/orders" : "v1/orders/" + Uri.EscapeDataString(id));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
        if (chave != null) req.Headers.Add("X-Idempotency-Key", chave);
        if (corpo != null) req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
                throw new FalhaPagamento($"Mercado Pago respondeu HTTP {(int)response.StatusCode}. Tente consultar novamente; se persistir, avise o restaurante.");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (HttpRequestException) { throw new FalhaPagamento("Não foi possível comunicar com o Mercado Pago. Tente novamente."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new FalhaPagamento("A comunicação demorou. Consulte o pedido antes de tentar novamente."); }
        catch (JsonException) { throw new FalhaPagamento("Resposta inválida do provedor."); }
    }
}
