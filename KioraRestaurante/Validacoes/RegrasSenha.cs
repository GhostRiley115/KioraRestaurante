namespace KioraRestaurante.Validacoes;

// Centraliza a regra usada pelos DTOs, ViewModels e formulários.
// Não é aplicada ao login: senhas antigas continuam funcionando.
public static class RegrasSenha
{
    public const int TamanhoMinimo = 8;
    public const string Mensagem = "Use pelo menos 8 caracteres, incluindo uma letra e um número.";

    // Exige letra (inclusive acentuada) e número. Símbolos e espaços são permitidos.
    // O tamanho mínimo é verificado separadamente pelo atributo MinLength.
    public const string Padrao = @"(?=[\s\S]*\p{L})(?=[\s\S]*[0-9])[\s\S]+";
}
