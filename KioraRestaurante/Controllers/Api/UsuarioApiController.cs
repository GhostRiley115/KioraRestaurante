
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers.Api
{
    // Controller responsável pelos endpoints de usuário do aplicativo.
    [ApiController]
    [Route("api/usuarios")]
    public class UsuarioApiController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuarioApiController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        // POST: api/usuarios/cadastro
        [HttpPost("cadastro")]
        public IActionResult Cadastrar([FromBody] UsuarioCadastroDTO dto)
        {
            // Verifica se o e-mail já está cadastrado.
            if (_usuarioService.EmailExiste(dto.Email))
            {
                return Conflict(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }

            // Reutiliza a regra de cadastro já existente.
            var usuario = _usuarioService.Cadastrar(dto);

            // Retorna os dados públicos do usuário criado.
            return StatusCode(201, new
            {
                sucesso = true,
                mensagem = "Usuário cadastrado com sucesso.",
                usuario
            });
        }


        // POST: api/usuarios/login
        [HttpPost("login")]
        public IActionResult Login([FromBody] UsuarioLoginDTO dto)
        {
            // Reutiliza a autenticação já implementada no serviço.
            var usuario = _usuarioService.Autenticar(dto);

            // Retorna erro quando as credenciais forem inválidas.
            if (usuario == null)
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "E-mail ou senha inválidos."
                });
            }

            // Retorna apenas os dados públicos do usuário autenticado.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Login realizado com sucesso.",
                usuario
            });
        }
    }
}
