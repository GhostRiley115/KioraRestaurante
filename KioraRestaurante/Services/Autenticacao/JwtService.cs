
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KioraRestaurante.Services.Autenticacao
{
    // Responsável por gerar tokens JWT para usuários autenticados.
    public class JwtService : IJwtService
    {
        private readonly JwtOptions _configuration;

        public JwtService(IOptions<JwtOptions> configuration)
        {
            _configuration = configuration.Value;
        }

        public string GerarToken(UsuarioResponseDTO usuario)
        {
            // Recupera a chave secreta do User Secrets.
            var chaveSecreta = _configuration.ChaveSecreta;

            if (!_configuration.Valida)
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

            var claims = new[]
{
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Tipo.ToString()),

                // Identifica a versão da sessão para validar operações sensíveis.
                new Claim("VersaoSessao", usuario.VersaoSessao.ToString())
            };

            // Monta o token com emissor, público e validade.
            var token = new JwtSecurityToken(
                issuer: _configuration.Emissor,
                audience: _configuration.Publico,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_configuration.ValidadeMinutos),
                signingCredentials: credenciais
            );

            // Converte o token para uma string que o aplicativo poderá receber.
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
