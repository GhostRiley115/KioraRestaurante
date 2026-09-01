// Permite utilizar a classe Usuario, que representa o usuário salvo no banco de dados.
using KioraRestaurante.Models;

// Permite utilizar a interface IUsuarioServices,
// responsável pelas operações relacionadas aos usuários.
using KioraRestaurante.Services.Interfaces;

// Permite utilizar os ViewModels utilizados pelo cadastro e pelo login.
using KioraRestaurante.ViewModels;

// Permite utilizar Controller, IActionResult, HttpGet, HttpPost
// e outros recursos do ASP.NET Core MVC.
using Microsoft.AspNetCore.Mvc;

// Permite utilizar os recursos de autenticação do ASP.NET Core.
using Microsoft.AspNetCore.Authentication;

// Permite utilizar o esquema de autenticação por Cookie.
using Microsoft.AspNetCore.Authentication.Cookies;

// Permite criar as informações que serão armazenadas
// dentro do Cookie de autenticação.
using System.Security.Claims;


namespace KioraRestaurante.Controllers
{
    // Controller responsável pelas operações relacionadas à conta do usuário.
    //
    // Neste Controller ficarão as ações de:
    // - Cadastro
    // - Login
    // - Recuperação de senha
    // - Logout
    public class AccountController : Controller
    {
        // ================================================================
        // SERVICE DE USUÁRIO
        // ================================================================

        // Guarda uma referência para a interface IUsuarioServices.
        //
        // O Controller não acessa o banco de dados diretamente.
        // Ele utiliza o Service para executar as regras relacionadas
        // aos usuários.
        private readonly IUsuarioServices _usuarioServices;


        // ================================================================
        // CONSTRUTOR
        // ================================================================

        // O ASP.NET Core utiliza a Injeção de Dependência para fornecer
        // uma instância de IUsuarioServices.
        public AccountController(IUsuarioServices usuarioServices)
        {
            // Guarda o Service recebido na variável privada.
            _usuarioServices = usuarioServices;
        }


        // ================================================================
        // CADASTRO - GET
        // ================================================================

        // [HttpGet] indica que este método responde a requisições HTTP GET.
        //
        // O GET é utilizado para solicitar a tela de cadastro.
        [HttpGet]
        public IActionResult Cadastro()
        {
            // Retorna a View de cadastro.
            return View();
        }


        // ================================================================
        // CADASTRO - POST
        // ================================================================

        // [HttpPost] indica que este método será executado
        // quando o formulário de cadastro for enviado.
        [HttpPost]
        public IActionResult Cadastro(CadastroUsuarioViewModel model)
        {
            // ============================================================
            // VALIDAÇÃO DOS DADOS
            // ============================================================

            // Verifica se os dados enviados pelo formulário
            // passaram pelas validações do ViewModel.
            if (!ModelState.IsValid)
            {
                // Retorna uma resposta de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Verifique os dados informados."
                });
            }


            // ============================================================
            // VERIFICAÇÃO DE E-MAIL
            // ============================================================

            // Verifica se já existe um usuário cadastrado
            // utilizando o e-mail informado.
            if (_usuarioServices.EmailExiste(model.Email))
            {
                // Retorna uma resposta de erro informando
                // que o e-mail já está cadastrado.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }


            // ============================================================
            // CONVERSÃO DO VIEWMODEL PARA USUARIO
            // ============================================================

            // Cria uma nova entidade Usuario utilizando
            // os dados recebidos do formulário.
            var usuario = new Usuario
            {
                // Define o nome do usuário.
                Nome = model.Nome,

                // Define o e-mail do usuário.
                Email = model.Email,

                // Define a senha recebida.
                //
                // O Service será responsável pelo tratamento
                // da senha antes de salvá-la no banco.
                Senha = model.Senha
            };


            // ============================================================
            // CADASTRO
            // ============================================================

            // Envia o usuário para o UsuarioServices.
            _usuarioServices.Cadastrar(usuario);


            // ============================================================
            // RESPOSTA DE SUCESSO
            // ============================================================

            // Retorna uma resposta que será interpretada
            // pelo JavaScript do formulário.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Conta criada com sucesso!"
            });
        }


        // ================================================================
        // LOGIN - POST
        // ================================================================

        // [HttpPost] indica que este método será executado
        // quando o formulário de login for enviado.
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // ============================================================
            // VALIDAÇÃO DOS DADOS
            // ============================================================

            // Verifica se o e-mail e a senha passaram
            // pelas validações do LoginViewModel.
            if (!ModelState.IsValid)
            {
                // Retorna uma resposta de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe seu e-mail e sua senha."
                });
            }


            // ============================================================
            // AUTENTICAÇÃO
            // ============================================================

            // Procura o usuário pelo e-mail e verifica a senha.
            var usuario = _usuarioServices.Autenticar(
                model.Email,
                model.Senha
            );


            // ============================================================
            // LOGIN INVÁLIDO
            // ============================================================

            // Verifica se o usuário não foi encontrado
            // ou se a senha informada está incorreta.
            if (usuario == null)
            {
                // Retorna uma mensagem de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "E-mail ou senha incorretos."
                });
            }


            // ============================================================
            // INFORMAÇÕES DO USUÁRIO
            // ============================================================

            // Cria as informações que serão armazenadas
            // no Cookie de autenticação.
            var claims = new List<Claim>
            {
                // Guarda o ID do usuário.
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()
                ),

                // Guarda o nome do usuário.
                //
                // É esta informação que posteriormente
                // poderemos utilizar no menu.
                new Claim(
                    ClaimTypes.Name,
                    usuario.Nome
                ),

                // Guarda o e-mail do usuário.
                new Claim(
                    ClaimTypes.Email,
                    usuario.Email
                )
            };


            // ============================================================
            // IDENTIDADE DO USUÁRIO
            // ============================================================

            // Cria a identidade utilizando o esquema
            // de autenticação por Cookie.
            var identidade = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );


            // ============================================================
            // PRINCIPAL DO USUÁRIO
            // ============================================================

            // Cria o objeto que representa o usuário autenticado.
            var principal = new ClaimsPrincipal(identidade);


            // ============================================================
            // CRIAR COOKIE DE AUTENTICAÇÃO
            // ============================================================

            // Cria o Cookie de autenticação no navegador.
            //
            // A partir deste momento o ASP.NET Core poderá reconhecer
            // o usuário nas próximas requisições.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );


            // ============================================================
            // LOGIN REALIZADO COM SUCESSO
            // ============================================================

            // Retorna os dados para o JavaScript.
            return Ok(new
            {
                // Informa que o login foi realizado com sucesso.
                sucesso = true,

                // Mensagem exibida ao usuário.
                mensagem = $"Bem-vindo(a), {usuario.Nome}!",

                // Envia o nome do usuário para o JavaScript.
                nome = usuario.Nome
            });
        }


        // ================================================================
        // LOGOUT - GET
        // ================================================================

        // [HttpGet] indica que esta ação responde a uma requisição GET.
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // ============================================================
            // REMOVER COOKIE DE AUTENTICAÇÃO
            // ============================================================

            // Remove o Cookie que mantém o usuário autenticado.
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );


            // ============================================================
            // RETORNO
            // ============================================================

            // Depois de sair, retorna para a página inicial.
            return RedirectToAction("Index", "Home");
        }
    }
}