using KioraRestaurante.DTOs.Usuario;

namespace KioraRestaurante.Services.Interfaces
{
    // Define as operações relacionadas à geração de tokens JWT.
    public interface IJwtService
    {
        // Gera um token de autenticação para o usuário informado.
        string GerarToken(UsuarioResponseDTO usuario);
    }
}
