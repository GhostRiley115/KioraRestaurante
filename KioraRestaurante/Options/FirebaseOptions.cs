namespace KioraRestaurante.Options;

// Configurações da integração com o Firebase.
// As credenciais privadas são externas ao código.
public sealed class FirebaseOptions
{
    public const string Secao = "Firebase";

    public bool Ativado { get; set; } = false;

    public string ProjetoId { get; set; } = string.Empty;
}