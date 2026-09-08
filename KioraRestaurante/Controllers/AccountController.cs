// Permite utilizar a classe Usuario, que representa o usuário salvo no banco de dados.
using KioraRestaurante.Models;

//Permite utilizar a interface IUsuarioServices,
//responsável pelas operações relacionadas aos usuários.
using KioraRestaurante.Services.Interfaces;

//Permite utilizar os ViewModels utilizados pelo cadastro e pelo login.
using KioraRestaurante.ViewModels;

//Permite utilizar Controller, IActionResult, HttpGet, HttpPost
//e outros recursos do ASP.NET Core MVC.
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

//Permite utilizar os recursos de autenticação do ASP.NET Core.
using Microsoft.AspNetCore.Authentication;

//Permite utilizar o esquema de autenticação por Cookie.
using Microsoft.AspNetCore.Authentication.Cookies;

//Permite criar as informações que serão armazenadas
//dentro do Cookie de autenticação.
using System.Security.Claims;

namespace KioraRestaurante.Controllers
{
    // Controller responsável pelas operações relacionadas à conta do usuário.
    //Neste Controller ficarão as ações de:
    //Cadastro
    //Login
    //Recuperação de senha
    //Logout
    public class AccountController : Controller
    {
        //SERVICE DE USUÁRIO
        //Guarda uma referência para a interface IUsuarioServices.
        //O Controller não acessa o banco de dados diretamente.
        //Ele utiliza o Service para executar as regras relacionadas
        //aos usuários.
        private readonly IUsuarioServices _usuarioServices;

        //CONSTRUTOR
        //O ASP.NET Core utiliza a Injeção de Dependência para fornecer
        //uma instância de IUsuarioServices.
        public AccountController(IUsuarioServices usuarioServices)
        {
            //Guarda o Service recebido na variável privada.
            _usuarioServices = usuarioServices;
        }

        //CADASTRO - GET
        //[HttpGet] indica que este método responde a requisições HTTP GET.
        //O GET é utilizado para solicitar a tela de cadastro.
        [HttpGet]
        public IActionResult Cadastro()
        {
            //Retorna a View de cadastro.
            return View();
        }

        //CADASTRO - POST
        //[HttpPost] indica que este método será executado
        //quando o formulário de cadastro for enviado.
        [HttpPost]
        public IActionResult Cadastro(CadastroUsuarioViewModel model)
        {
            //VALIDAÇÃO DOS DADOS
            //Verifica se os dados enviados pelo formulário
            //passaram pelas validações do ViewModel.
            if (!ModelState.IsValid)
            {
                //Obtém a primeira mensagem de validação encontrada.
                //Dessa forma, o sistema poderá apresentar ao usuário
                //mensagens específicas como:
                //"O nome é obrigatório."
                //"O e-mail é obrigatório."
                //"Digite um e-mail válido."
                //"A senha é obrigatória."
                //"As senhas não são iguais."
                var mensagem = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                // Retorna uma resposta de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,
                    // Envia a mensagem específica para o JavaScript.
                    // Caso nenhuma mensagem seja encontrada,
                    // utiliza uma mensagem padrão.
                    mensagem = mensagem ?? "Verifique os dados informados."
                });
            }

            //VERIFICAÇÃO DE E-MAIL
            //Verifica se já existe um usuário cadastrado
            //utilizando o e-mail informado.
            if (_usuarioServices.EmailExiste(model.Email))
            {
                //Retorna uma resposta de erro informando
                //que o e-mail já está cadastrado.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }

            //CONVERSÃO DO VIEWMODEL PARA USUARIO
            //Cria uma nova entidade Usuario utilizando
            //os dados recebidos do formulário.
            var usuario = new Usuario
            {
                //Define o nome do usuário.
                Nome = model.Nome,

                //Define o e-mail do usuário.
                Email = model.Email,

                // Define a senha recebida.
                // O Service será responsável pelo tratamento
                // da senha antes de salvá-la no banco.
                Senha = model.Senha
            };

            //CADASTRO
            //Envia o usuário para o UsuarioServices.
            _usuarioServices.Cadastrar(usuario);

            //RESPOSTA DE SUCESSO
            // Retorna uma resposta que será interpretada
            // pelo JavaScript do formulário.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Conta criada com sucesso!"
            });
        }

        //LOGIN - POST
        //[HttpPost] indica que este método será executado
        //quando o formulário de login for enviado.
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            //VALIDAÇÃO DOS DADOS

            //Verifica se o e-mail e a senha passaram
            //pelas validações do LoginViewModel.
            if (!ModelState.IsValid)
            {
                //Obtém a primeira mensagem de validação encontrada.
                //Dessa forma, o sistema poderá apresentar ao usuário
                //mensagens específicas como:
                //"Informe seu e-mail."
                //"Informe um e-mail válido."
                //"Informe sua senha."
                var mensagem = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                //Retorna uma resposta de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,

                    //Envia a mensagem específica para o JavaScript.
                    //Caso nenhuma mensagem seja encontrada,
                    //utiliza uma mensagem padrão.
                    mensagem = mensagem ?? "Informe seu e-mail e sua senha."
                });
            }

            //AUTENTICAÇÃO
            //Procura o usuário pelo e-mail e verifica a senha.
            var usuario = _usuarioServices.Autenticar(
                model.Email,
                model.Senha
            );

            //LOGIN INVÁLIDO
            //Verifica se o usuário não foi encontrado
            //ou se a senha informada está incorreta.
            if (usuario == null)
            {
                //Retorna uma mensagem de erro para o JavaScript.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "E-mail ou senha incorretos."
                });
            }

            //INFORMAÇÕES DO USUÁRIO
            //Cria as informações que serão armazenadas
            //no Cookie de autenticação.
            var claims = new List<Claim>
            {
                //Guarda o ID do usuário.
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()
                ),

                //Guarda o nome do usuário.
                //É esta informação que posteriormente
                //poderemos utilizar no menu.
                new Claim(
                    ClaimTypes.Name,
                    usuario.Nome
                ),

                //Guarda o e-mail do usuário.
                new Claim(
                    ClaimTypes.Email,
                    usuario.Email
                )
            };

            //IDENTIDADE DO USUÁRIO
            //Cria a identidade utilizando o esquema
            //e autenticação por Cookie.
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
        // RECUPERAÇÃO DE SENHA
        // ================================================================

        // Recebe a solicitação de recuperação de senha.
        //
        // O usuário informa seu e-mail no modal
        // "Esqueci minha senha".
        [HttpPost]
        public IActionResult EsqueciSenha(
            [FromForm] EsqueciSenhaViewModel model
        )
        {
            // ============================================================
            // VALIDAÇÃO DOS DADOS
            // ============================================================

            // Verifica se o e-mail informado passou
            // pelas validações do ViewModel.
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Informe um e-mail válido."
                });
            }


            // ============================================================
            // LOCALIZAR USUÁRIO
            // ============================================================

            // Procura o usuário utilizando o e-mail informado.
            var usuario = _usuarioServices.BuscarPorEmail(
                model.Email
            );


            // ============================================================
            // VERIFICAR USUÁRIO
            // ============================================================

            // Por segurança, não informamos ao usuário
            // se o e-mail está ou não cadastrado.
            //
            // Isso evita que alguém possa descobrir
            // quais e-mails possuem cadastro no sistema.
            if (usuario == null)
            {
                return Ok(new
                {
                    sucesso = true,
                    mensagem =
                        "Se o e-mail estiver cadastrado, você receberá um link para redefinir sua senha."
                });
            }


            // ============================================================
            // GERAR TOKEN
            // ============================================================

            // Gera um token temporário para recuperação da senha.
            //
            // O Service também define o prazo de validade
            // desse token.
            string token =
                _usuarioServices.GerarTokenRecuperacao(usuario);


            // ============================================================
            // RESPOSTA
            // ============================================================

            // Neste momento o token já foi gerado e armazenado
            // no banco de dados.
            //
            // O envio do token por e-mail será implementado
            // posteriormente.
            //
            // Por segurança, o token NÃO é enviado para o navegador.
            return Ok(new
            {
                sucesso = true,
                mensagem =
                    "Se o e-mail estiver cadastrado, você receberá um link para redefinir sua senha."
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


        // ================================================================
        // MEU PERFIL - GET
        // ================================================================

        // Permite acesso somente para usuários autenticados.
        [Authorize]

        // [HttpGet] indica que esta ação responde a uma requisição GET.
        [HttpGet]
        public IActionResult MeuPerfil()
        {
            // Obtém o e-mail do usuário que está logado.
            var email = User.FindFirstValue(ClaimTypes.Email);

            // Verifica se o e-mail foi encontrado no Cookie de autenticação.
            if (string.IsNullOrEmpty(email))
            {
                // Caso não encontre o e-mail, retorna para a página inicial.
                return RedirectToAction("Index", "Home");
            }

            // Procura o usuário no banco de dados utilizando o e-mail.
            var usuario = _usuarioServices.BuscarPorEmail(email);

            // Verifica se o usuário foi encontrado no banco.
            if (usuario == null)
            {
                // Caso o usuário não exista mais no banco,
                // retorna para a página inicial.
                return RedirectToAction("Index", "Home");
            }

            // Envia o usuário encontrado para a View MeuPerfil.cshtml.
            return View(usuario);
        }

        // ================================================================
        // EDITAR PERFIL - GET
        // ================================================================

        // Esta ação será responsável por carregar
        // os dados atuais do usuário para a edição do perfil.
        //
        // O usuário precisa estar autenticado para acessar
        // esta funcionalidade.
        [Authorize]
        [HttpGet]
        public IActionResult EditarPerfil()
        {
            // Recupera o e-mail armazenado no Cookie
            // de autenticação do usuário atualmente logado.
            var email = User.FindFirstValue(ClaimTypes.Email);


            // Verifica se o e-mail não foi encontrado
            // dentro do Cookie de autenticação.
            if (string.IsNullOrEmpty(email))
            {
                // Caso não exista um e-mail válido,
                // retorna o usuário para a página inicial.
                return RedirectToAction("Index", "Home");
            }


            // Busca no banco de dados o usuário correspondente
            // ao e-mail encontrado no Cookie.
            var usuario = _usuarioServices.BuscarPorEmail(email);


            // Verifica se o usuário foi encontrado no banco.
            if (usuario == null)
            {
                // Caso o usuário não exista mais no banco,
                // retorna para a página inicial.
                return RedirectToAction("Index", "Home");
            }


            // Cria um ViewModel específico para edição.
            //
            // Não utilizamos a entidade Usuario diretamente
            // no formulário de edição.
            var model = new EditarPerfilViewModel
            {
                // Preenche o campo Nome com o nome
                // atualmente cadastrado.
                Nome = usuario.Nome,

                // Preenche o campo Email com o e-mail
                // atualmente cadastrado.
                Email = usuario.Email
            };


            // Envia o ViewModel para a View de edição.
            return View(model);
        }

        // ================================================================
        // EDITAR PERFIL - POST
        // ================================================================

        // Esta ação recebe os dados enviados pelo formulário
        // de edição do perfil.
        //
        // O usuário precisa estar autenticado para realizar
        // esta operação.
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditarPerfil(
            EditarPerfilViewModel model
        )
        {
            // ============================================================
            // VALIDAÇÃO DO FORMULÁRIO
            // ============================================================

            // Verifica se os dados enviados pelo formulário
            // passaram pelas validações do ViewModel.
            if (!ModelState.IsValid)
            {
                // Procura a primeira mensagem de erro
                // encontrada nas validações.
                var mensagem = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();


                // Retorna uma resposta informando
                // que os dados não são válidos.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = mensagem ??
                               "Verifique os dados informados."
                });
            }


            // ============================================================
            // IDENTIFICAR USUÁRIO LOGADO
            // ============================================================

            // Recupera o e-mail atualmente armazenado
            // no Cookie de autenticação.
            var emailAtual = User.FindFirstValue(
                ClaimTypes.Email
            );


            // Verifica se o e-mail foi encontrado.
            if (string.IsNullOrEmpty(emailAtual))
            {
                // Caso não exista um e-mail válido,
                // informa que não foi possível identificar
                // o usuário.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário."
                });
            }


            // Busca o usuário atualmente logado
            // no banco de dados.
            var usuario = _usuarioServices.BuscarPorEmail(
                emailAtual
            );


            // Verifica se o usuário realmente existe.
            if (usuario == null)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Usuário não encontrado."
                });
            }


            // ============================================================
            // VERIFICAR E-MAIL DUPLICADO
            // ============================================================

            // Verifica se o novo e-mail informado
            // já pertence a outro usuário.
            var emailJaExiste =
                _usuarioServices.EmailExisteParaOutroUsuario(
                    model.Email,
                    usuario.Id
                );


            // Caso o e-mail já pertença a outra conta,
            // impede a alteração.
            if (emailJaExiste)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }


            // ============================================================
            // ATUALIZAR DADOS DO USUÁRIO
            // ============================================================

            // Atualiza somente os dados permitidos
            // pela edição do perfil.
            usuario.Nome = model.Nome;
            usuario.Email = model.Email;


            // Envia o usuário atualizado para o Service,
            // que será responsável por salvar os dados
            // no banco de dados.
            _usuarioServices.AtualizarPerfil(usuario);


            // ============================================================
            // ATUALIZAR COOKIE DE AUTENTICAÇÃO
            // ============================================================

            // Depois de alterar o nome ou o e-mail,
            // precisamos atualizar também o Cookie de autenticação.
            //
            // Isso é importante porque o menu do site utiliza
            // o nome armazenado no Cookie.
            //
            // Além disso, o MeuPerfil utiliza o e-mail armazenado
            // no Cookie para localizar o usuário no banco.


            // Cria uma nova lista de Claims
            // com os dados atualizados.
            var claims = new List<Claim>
            {
                // Mantém o identificador do usuário.
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.Id.ToString()
                ),

                // Atualiza o nome armazenado no Cookie.
                new Claim(
                    ClaimTypes.Name,
                    usuario.Nome
                ),

                // Atualiza o e-mail armazenado no Cookie.
                new Claim(
                    ClaimTypes.Email,
                    usuario.Email
                )
            };


            // Cria uma nova identidade utilizando
            // o mesmo esquema de autenticação por Cookie.
            var identidade = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );


            // Cria um novo usuário autenticado
            // utilizando a identidade atualizada.
            var principal = new ClaimsPrincipal(identidade);


            // Substitui o Cookie atual pelo novo Cookie,
            // contendo o nome e o e-mail atualizados.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );


            // ============================================================
            // RESPOSTA DE SUCESSO
            // ============================================================

            // Retorna uma resposta de sucesso para o JavaScript.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Perfil atualizado com sucesso!",
                nome = usuario.Nome
            });
        }

        // ================================================================
        // ALTERAR SENHA - POST
        // ================================================================

        // Esta ação recebe os dados enviados pelo formulário
        // de alteração de senha.
        //
        // O usuário precisa estar autenticado para realizar
        // esta operação.
        [Authorize]
        [HttpPost]
        public IActionResult AlterarSenha(
            AlterarSenhaViewModel model
        )
        {
            // ============================================================
            // VALIDAR DADOS DO FORMULÁRIO
            // ============================================================

            // Verifica se os dados enviados passaram
            // pelas validações definidas no ViewModel.
            if (!ModelState.IsValid)
            {
                // Procura a primeira mensagem de erro
                // encontrada nas validações.
                var mensagem = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();


                // Retorna uma resposta informando
                // que os dados enviados são inválidos.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = mensagem ??
                               "Verifique os dados informados."
                });
            }


            // ============================================================
            // IDENTIFICAR USUÁRIO LOGADO
            // ============================================================

            // Recupera o identificador do usuário
            // armazenado no Cookie de autenticação.
            var usuarioIdString = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );


            // Verifica se o identificador foi encontrado.
            if (string.IsNullOrEmpty(usuarioIdString))
            {
                // Caso o identificador não exista,
                // não será possível localizar o usuário.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Não foi possível identificar o usuário."
                });
            }


            // ============================================================
            // CONVERTER ID DO USUÁRIO
            // ============================================================

            // Converte o identificador recebido do Cookie
            // de texto para número inteiro.
            if (!int.TryParse(
                usuarioIdString,
                out int usuarioId
            ))
            {
                // Caso o valor não possa ser convertido,
                // interrompe a operação.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Identificação do usuário inválida."
                });
            }


            // ============================================================
            // ALTERAR SENHA
            // ============================================================

            // Envia os dados para o Service responsável
            // pela alteração da senha.
            //
            // O Service irá:
            //
            // 1. Procurar o usuário.
            // 2. Verificar a senha atual.
            // 3. Criar o hash da nova senha.
            // 4. Salvar a nova senha no banco.
            var senhaAlterada = _usuarioServices.AlterarSenha(
                usuarioId,
                model.SenhaAtual,
                model.NovaSenha
            );


            // ============================================================
            // VERIFICAR RESULTADO
            // ============================================================

            // Caso a senha atual esteja incorreta,
            // o Service retornará false.
            if (!senhaAlterada)
            {
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "A senha atual está incorreta."
                });
            }


            // ============================================================
            // RESPOSTA DE SUCESSO
            // ============================================================

            // Retorna uma resposta de sucesso para o JavaScript.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Senha alterada com sucesso!"
            });
        }
    }

}