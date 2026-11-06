namespace KioraRestaurante.Options;
public class MercadoPagoConfig
{
    public bool Ativado { get; set; }
    public bool ModoTeste { get; set; } = true;
    public string AccessToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string VendedorId { get; set; } = "";
    public string ApplicationId { get; set; } = "";
    public string UrlPublica { get; set; } = "";
}