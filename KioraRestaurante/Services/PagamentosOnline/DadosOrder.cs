using System.Globalization;
using System.Text.Json;

namespace KioraRestaurante.Services.PagamentosOnline;

public static class DadosOrder
{
    public static string Texto(JsonElement e, string nome) => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(nome, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : "";
    public static decimal? Valor(JsonElement e, string nome)
    {
        var s = Texto(e, nome);
        if (s.Length == 0) return null;
        if (decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) && v >= 0) return v;
        throw new FalhaPagamento("O valor recebido do provedor precisa de conferência.");
    }
    public static JsonElement[] Transacoes(JsonElement e, string nome) => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty("transactions", out var t) && t.ValueKind == JsonValueKind.Object
        && t.TryGetProperty(nome, out var lista) && lista.ValueKind == JsonValueKind.Array ? lista.EnumerateArray().ToArray() : [];
    public static bool Aprovada(JsonElement e, decimal valor) => Texto(e, "status") == "processed"
        && Texto(e, "status_detail") == "accredited" && Valor(e, "total_paid_amount") == valor;
    public static bool EncerradaSemPagamento(JsonElement e) => Texto(e, "status") is "canceled" or "failed"
        && (Valor(e, "total_paid_amount") ?? 0) == 0;
}
