namespace KioraRestaurante.DTOs.Usuario;

// Dados internos do envio: nunca são devolvidos ao navegador.
// O identificador permite cancelar somente o código cujo envio falhou.
public sealed record RecuperacaoSenhaGeradaDTO(string Email, string Codigo, string Identificador);
