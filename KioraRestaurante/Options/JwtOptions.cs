using System.Text;

namespace KioraRestaurante.Options;

// A mesma configuração é usada na emissão e na validação dos tokens do aplicativo.
public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public string ChaveSecreta { get; set; } = "";
    public string Emissor { get; set; } = "KioraRestauranteAPI";
    public string Publico { get; set; } = "AppKiora";
    public int ValidadeMinutos { get; set; } = 120;
    public bool Valida => !string.IsNullOrWhiteSpace(ChaveSecreta)
        && Encoding.UTF8.GetByteCount(ChaveSecreta) >= 32
        && !string.IsNullOrWhiteSpace(Emissor) && !string.IsNullOrWhiteSpace(Publico)
        && ValidadeMinutos is >= 5 and <= 120;
}
