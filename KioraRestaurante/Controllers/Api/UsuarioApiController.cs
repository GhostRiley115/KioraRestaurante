
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace KioraRestaurante.Controllers.Api
{
    // Controller responsável pelos endpoints de usuário do aplicativo.
    [ApiController]
    [Route("api/usuarios")]
    public class UsuarioApiController : ControllerBase
    {

        private readonly IUsuarioService _usuarioService;
        private readonly IJwtService _jwtService;

        public UsuarioApiController(
            IUsuarioService usuarioService,
            IJwtService jwtService)
        {
            _usuarioService = usuarioService;
            _jwtService = jwtService;
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

            // Gera o token JWT para o aplicativo mobile.
            var token = _jwtService.GerarToken(usuario);

            // Retorna o token e os dados públicos do usuário.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Login realizado com sucesso.",
                token,
                tipoToken = "Bearer",
                usuario
            });
        }


        // GET: api/usuarios/perfil
        // Retorna o perfil do usuário autenticado pelo JWT.
        [HttpGet("perfil")]
        [Microsoft.AspNetCore.Authorization.Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult Perfil()
        {
            // Recupera o ID do usuário a partir do token JWT.
            var idUsuarioTexto = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Verifica se o token contém um ID válido.
            if (!int.TryParse(idUsuarioTexto, out var idUsuario))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário autenticado."
                });
            }

            // Busca os dados atuais do usuário no banco de dados.
            var usuario = _usuarioService.BuscarPorId(idUsuario);

            // Impede o acesso ao perfil se o usuário não existir ou estiver inativo.
            if (usuario == null)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado ou inativo."
                });
            }

            // Retorna os dados públicos do usuário.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Perfil carregado com sucesso.",
                usuario
            });
        }


        // PUT: api/usuarios/perfil
        // Atualiza o nome e o e-mail do usuário autenticado.
        [Microsoft.AspNetCore.Authorization.Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("perfil")]
        public IActionResult AtualizarPerfil(
            [FromBody] UsuarioAtualizarPerfilDTO dto)
        {
            // Recupera o ID do usuário a partir do token JWT.
            var idUsuarioTexto = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Verifica se o token contém um ID válido.
            if (!int.TryParse(idUsuarioTexto, out var idUsuario))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário autenticado."
                });
            }

            // Impede que o e-mail seja utilizado por outra conta.
            if (_usuarioService.EmailExisteParaOutroUsuario(
                dto.Email, idUsuario))
            {
                return Conflict(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está sendo utilizado por outra conta."
                });
            }

            // Atualiza os dados utilizando o serviço existente.
            var atualizado = _usuarioService.AtualizarPerfil(
                idUsuario, dto);

            if (!atualizado)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }

            // Busca os dados atualizados no banco.
            var usuario = _usuarioService.BuscarPorId(idUsuario);

            if (usuario == null)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Perfil atualizado com sucesso!",
                usuario
            });
        }

    }
}
