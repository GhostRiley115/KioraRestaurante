using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels;

/*Permite utilizar Controller, IActionResult, HttpGet, HttpPost
e outros recursos do ASP.NET Core MVC.*/
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
//recursos de autenticação do ASP.NET Core.
using Microsoft.AspNetCore.Authentication;
//esquema de autenticação por Cookie.
using Microsoft.AspNetCore.Authentication.Cookies;
/*Permite criar as informações que serão armazenadas
dentro do Cookie de autenticação.*/
using System.Security.Claims;

namespace KioraRestaurante.Controllers
{
    //Controller responsável pelas operações relacionadas à conta do usuário.
    public class AccountController : Controller
    {
        //Guarda uma referência para a interface IUsuarioServices.
        private readonly IUsuarioService _usuarioService;

        //instância de IUsuarioServices.
        public AccountController(IUsuarioService usuarioService)
        {
            //Guarda o Service recebido na variável privada.
            _usuarioService = usuarioService;
        }

        /* ---- MÉTODOS AUXILIARES ---- */
        //Obtém o Id do usuário armazenado no cookie de autenticação.
        private int? ObterUsuarioId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(id, out int usuarioId))
                return null;

            return usuarioId;
        }

        /*Cria as informações do usuário que serão armazenadas
        no cookie de autenticação.*/
        private static ClaimsPrincipal CriarPrincipal(
            UsuarioResponseDTO usuario)
        {
            //Cria as informações que serão armazenadas no Cookie de autenticação.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Tipo.ToString())
            };

            //Cria a identidade utilizando o esquema e autenticação por Cookie.
            var identidade = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            //Cria o objeto que representa o usuário autenticado.
            return new ClaimsPrincipal(identidade);
        }

        //Retorna a primeira mensagem gerada pelas validações do formulário.
        private string ObterPrimeiroErroValidacao()
        {
            //Obtém a primeira mensagem de validação encontrada. Dessa forma, o sistema poderá apresentar mensagens ao usuário.
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault()
                ?? "Verifique os dados informados."; //Caso nenhuma mensagem seja encontrada, utiliza uma mensagem padrão.
        }

        /*---- CADASTRO ---- */
        [HttpGet]
        public IActionResult Cadastro()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Cadastro(CadastroUsuarioViewModel model)
        {
            //Verifica se os dados enviados pelo formulário passaram pelas validações do ViewModel.
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ObterPrimeiroErroValidacao()
                });
            }

            //Verifica se já existe um usuário cadastrado utilizando o e-mail informado.
            if (_usuarioService.EmailExiste(model.Email))
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }

            //Cria uma nova entidade Usuario utilizando os dados recebidos do formulário.
            var dto = new UsuarioCadastroDTO
            {
                Nome = model.Nome,
                Email = model.Email,
                Senha = model.Senha,
                ConfirmarSenha = model.ConfirmarSenha
            };

            _usuarioService.Cadastrar(dto);

            return Ok(new
            {
                sucesso = true,
                mensagem = "Conta criada com sucesso!"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ObterPrimeiroErroValidacao(),
                });
            }

            var dto = new UsuarioLoginDTO
            {
                Email = model.Email,
                Senha = model.Senha
            };

            //Procura o usuário pelo e-mail e verifica a senha.
            var usuario = _usuarioService.Autenticar(dto);

            if (usuario == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "E-mail ou senha incorretos."
                });
            }

            //Cria o objeto que representa o usuário autenticado.
            var principal = CriarPrincipal(usuario);

            /*Cria o Cookie de autenticação no navegador. A partir deste momento o ASP.NET Core
            poderá reconhecer o usuário nas próximas requisições.*/
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Ok(new
            {
                sucesso = true,
                mensagem = $"Bem-vindo(a), {usuario.Nome}!",
                nome = usuario.Nome
            });
        }

        [HttpPost]
        public IActionResult EsqueciSenha([FromForm] EsqueciSenhaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um e-mail válido."
                });
            }

            var dto = new UsuarioSolicitarRecuperacaoDTO
            {
                Email = model.Email
            };

            _usuarioService.GerarTokenRecuperacao(dto);

            /*Não informamos se o e-mail existe ou não,
            evitando a enumeração de contas cadastradas.*/
            return Ok(new
            {
                sucesso = true,
                mensagem =
                    "Se o e-mail estiver cadastrado, você receberá um link para redefinir sua senha."
            });
        }

        /* ---- LOGOUT ---- */
        //Permite acesso somente para usuários autenticados.
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            //Remove o Cookie que mantém o usuário autenticado.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            //Depois de sair, retorna para a página inicial.
            return RedirectToAction("Index", "Home");
        }

        /* ---- MEU PERFIL ---- */
        //Permite acesso somente para usuários autenticados.
        [Authorize]
        [HttpGet]
        public IActionResult MeuPerfil()
        {
            var usuarioId = ObterUsuarioId();

            //Verifica se o id foi encontrado no Cookie de autenticação.
            if (usuarioId == null)
                return RedirectToAction("Index", "Home");

            //Procura o usuário no banco de dados utilizando o id.
            var usuario = _usuarioService.BuscarPorId(usuarioId.Value);

            if (usuario == null)
                return RedirectToAction("Index", "Home");

            //Envia o usuário encontrado para a View MeuPerfil.cshtml.
            return View(usuario);
        }

        /* ---- EDITAR PERFIL ---- */
        //Esta ação será responsável por carregar os dados atuais do usuário para a edição do perfil.
        [Authorize]
        [HttpGet]
        public IActionResult EditarPerfil()
        {
            var usuarioId = ObterUsuarioId();

            if (usuarioId == null)
                return RedirectToAction("Index", "Home");

            var usuario = _usuarioService.BuscarPorId(usuarioId.Value);

            if (usuario == null)
                return RedirectToAction("Index", "Home");

            /*Cria um ViewModel específico para edição. Não utilizamos a entidade
            Usuario diretamenteno formulário de edição.*/
            var model = new EditarPerfilViewModel
            {
                //Preenche o campo Email com o e-mail, atualmente cadastrado.
                Nome = usuario.Nome,

                //Preenche o campo Email com o e-mail, atualmente cadastrado.
                Email = usuario.Email
            };

            // Envia o ViewModel para a View de edição.
            return View(model);
        }

        /*---- EDITAR PERFIL ---- */
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditarPerfil(EditarPerfilViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ObterPrimeiroErroValidacao()
                });
            }

            var usuarioId = ObterUsuarioId();

            if (usuarioId == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário."
                });
            }

            var dto = new UsuarioAtualizarPerfilDTO
            {
                Nome = model.Nome,
                Email = model.Email
            };

            var atualizado = _usuarioService.AtualizarPerfil(usuarioId.Value, dto);

            if (!atualizado)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }

            //Busca os dados atualizados para renovar o cookie.
            var usuario = _usuarioService.BuscarPorId(usuarioId.Value);

            if (usuario == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }

            var principal = CriarPrincipal(usuario);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults
                .AuthenticationScheme,principal);

            return Ok(new
            {
                sucesso = true,
                mensagem = "Perfil atualizado com sucesso!",
                nome = usuario.Nome
            });
        }

        /* ---- ALTERAR SENHA ---- */
        [Authorize]
        [HttpPost]
        public IActionResult AlterarSenha(AlterarSenhaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = ObterPrimeiroErroValidacao()
                });
            }

            var usuarioId = ObterUsuarioId();

            if (usuarioId == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário."
                });
            }

            var dto = new UsuarioAlterarSenhaDTO
            {
                SenhaAtual = model.SenhaAtual,
                NovaSenha = model.NovaSenha,
                ConfirmarNovaSenha = model.ConfirmarNovaSenha
            };

            var senhaAlterada = _usuarioService.AlterarSenha(usuarioId.Value, dto);

            if (!senhaAlterada)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "A senha Atual está incorreta."
                });
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Senha alterada com sucesso!"
            });
        }
    }
}