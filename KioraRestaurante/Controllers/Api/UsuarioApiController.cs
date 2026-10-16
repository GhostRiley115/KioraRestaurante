
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
    }
}
