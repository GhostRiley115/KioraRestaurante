
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace KioraRestaurante.Services
{
    // Responsável por gerar tokens JWT para usuários autenticados.
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GerarToken(UsuarioResponseDTO usuario)
        {
            // Recupera a chave secreta do User Secrets.
            var chaveSecreta = _configuration["Jwt:ChaveSecreta"];

            if (string.IsNullOrWhiteSpace(chaveSecreta))
            {
                throw new InvalidOperationException(
                    "A chave secreta do JWT não foi configurada.");
            }

            // Define a chave utilizada para assinar o token.
            var chave = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(chaveSecreta));

            var credenciais = new SigningCredentials(
                chave,
                SecurityAlgorithms.HmacSha256);

            // Identifica o usuário dentro do token.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Tipo.ToString())
            };

            // Monta o token com emissor, público e validade.
            var token = new JwtSecurityToken(
                issuer: "KioraRestauranteAPI",
                audience: "AppKiora",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credenciais
            );

            // Converte o token para uma string que o aplicativo poderá receber.
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
