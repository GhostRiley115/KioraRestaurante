
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.Services.Reservas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace KioraRestaurante.Controllers.Api
{
    // Controller responsável pelos endpoints de usuário do aplicativo.
    [ApiController]
    [Route("api/usuarios")]
    public class UsuarioApiController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IJwtService _jwtService;
        private readonly ReservaService _reservaService;
        private readonly IdentidadeReserva _identidadeReserva;

        public UsuarioApiController(
            IUsuarioService usuarioService,
            IJwtService jwtService,
            ReservaService reservaService,
            IdentidadeReserva identidadeReserva)
        {
            _usuarioService = usuarioService;
            _jwtService = jwtService;
            _reservaService = reservaService;
            _identidadeReserva = identidadeReserva;
        }

        // POST: api/usuarios/cadastro
        // Cadastra o usuário e associa as reservas anteriores do visitante.
        [HttpPost("cadastro")]
        public async Task<IActionResult> Cadastrar(
            [FromBody] UsuarioCadastroDTO dto)
        {
            if (_usuarioService.EmailExiste(dto.Email))
            {
                return Conflict(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }

            var usuario = _usuarioService.Cadastrar(dto);

            // Recupera a identidade do visitante enviada pelo aplicativo
            // ou armazenada no cookie do site.
            var acesso = _identidadeReserva.Obter(HttpContext);

            // Se existir uma chave válida, vincula as reservas à nova conta.
            if (acesso.VisitanteHash != null)
            {
                await _reservaService.VincularReservasVisitante(
                    usuario.Id,
                    acesso.VisitanteHash,
                    HttpContext.RequestAborted);
            }

            return StatusCode(201, new
            {
                sucesso = true,
                mensagem = "Usuário cadastrado com sucesso.",
                usuario
            });
        }

        // POST: api/usuarios/login
        // Autentica o usuário e associa suas reservas anteriores de visitante.
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] UsuarioLoginDTO dto)
        {
            var usuario = _usuarioService.Autenticar(dto);

            if (usuario == null)
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "E-mail ou senha inválidos."
                });
            }

            // Recupera a chave de visitante antes de concluir o login.
            var acesso = _identidadeReserva.Obter(HttpContext);

            // Associa as reservas realizadas anteriormente à conta autenticada.
            if (acesso.VisitanteHash != null)
            {
                await _reservaService.VincularReservasVisitante(
                    usuario.Id,
                    acesso.VisitanteHash,
                    HttpContext.RequestAborted);
            }

            var token = _jwtService.GerarToken(usuario);

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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("perfil")]
        public IActionResult Perfil()
        {
            var idUsuarioTexto = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idUsuarioTexto, out var idUsuario))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário autenticado."
                });
            }

            var usuario = _usuarioService.BuscarPorId(idUsuario);

            if (usuario == null)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado ou inativo."
                });
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Perfil carregado com sucesso.",
                usuario
            });
        }

        // PUT: api/usuarios/perfil
        // Atualiza o nome e o e-mail do usuário autenticado.
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("perfil")]
        public IActionResult AtualizarPerfil(
            [FromBody] UsuarioAtualizarPerfilDTO dto)
        {
            var idUsuarioTexto = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idUsuarioTexto, out var idUsuario))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário autenticado."
                });
            }

            if (_usuarioService.EmailExisteParaOutroUsuario(
                dto.Email, idUsuario))
            {
                return Conflict(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está sendo utilizado por outra conta."
                });
            }

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

        // POST: api/usuarios/alterar-senha
        // Altera a senha do usuário autenticado pelo JWT.
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("alterar-senha")]
        public IActionResult AlterarSenha(
            [FromBody] UsuarioAlterarSenhaDTO dto)
        {
            var idUsuarioTexto = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            var versaoTexto = User.FindFirst("VersaoSessao")?.Value;

            if (!int.TryParse(idUsuarioTexto, out var idUsuario) ||
                !Guid.TryParse(versaoTexto, out var versaoSessao) ||
                versaoSessao == Guid.Empty)
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar a sessão do usuário."
                });
            }

            var novaVersao = _usuarioService.AlterarSenha(
                idUsuario,
                dto,
                versaoSessao);

            if (novaVersao == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "A senha atual está incorreta ou sua sessão expirou."
                });
            }

            var usuario = _usuarioService.BuscarPorId(idUsuario);

            if (usuario == null)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }

            var token = _jwtService.GerarToken(usuario);

            return Ok(new
            {
                sucesso = true,
                mensagem = "Senha alterada com sucesso!",
                token,
                tipoToken = "Bearer",
                usuario
            });
        }

        // DELETE: api/usuarios/conta
        // Desativa a conta do usuário autenticado.
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpDelete("conta")]
        public IActionResult DesativarConta()
        {
            var idUsuarioTexto = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idUsuarioTexto, out var idUsuario))
            {
                return Unauthorized(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário autenticado."
                });
            }

            var desativado = _usuarioService.DesativarConta(idUsuario);

            if (!desativado)
            {
                return NotFound(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado ou conta já desativada."
                });
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Sua conta foi desativada com sucesso."
            });
        }
    }
}
