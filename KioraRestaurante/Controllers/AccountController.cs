// Permite utilizar a classe Usuario, que representa o usuário que será salvo no banco de dados.
using KioraRestaurante.Models;

// Permite utilizar a interface IUsuarioServices, responsável por definir as operações relacionadas aos usuários.
using KioraRestaurante.Services.Interfaces;

// Permite utilizar o CadastroUsuarioViewModel, que recebe e valida os dados enviados pelo formulário de cadastro.
using KioraRestaurante.ViewModels;

// Permite utilizar Controller, IActionResult, HttpGet, HttpPost e outros recursos do ASP.NET Core MVC.
using Microsoft.AspNetCore.Mvc;


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
        // SERVICE DE USUÁRIO

        // Guarda uma referência para a interface IUsuarioServices.

        // O Controller não acessa o banco de dados diretamente.
        // Ele utiliza o Service para executar as regras relacionadas aos usuários.
        private readonly IUsuarioServices _usuarioServices;


        // CONSTRUTOR

        // O ASP.NET Core utiliza a Injeção de Dependência para fornecer uma instância de IUsuarioServices.        
        // Dessa forma, o Controller consegue utilizar os métodos do UsuarioServices sem precisar criar a classe manualmente.
        public AccountController(IUsuarioServices usuarioServices)
        {
            // Guarda o Service recebido na variável privada para que possa ser utilizado pelos métodos do Controller.
            _usuarioServices = usuarioServices;
        }

        // CADASTRO - GET


        // [HttpGet] indica que este método responde a requisições HTTP do tipo GET.

        // O GET é utilizado para solicitar/exibir a tela ou formulário de cadastro.
        [HttpGet]
        public IActionResult Cadastro()
        {
            // Retorna a View de cadastro.            
            // Como não informamos o nome da View,o ASP.NET Core procurará por: Views/Account/Cadastro.cshtml
            return View();
        }

        // CADASTRO - POST

        // [HttpPost] indica que este método será executado quando o formulário de cadastro for enviado.
        [HttpPost]
        public IActionResult Cadastro(CadastroUsuarioViewModel model)
        {
            // VALIDAÇÃO DOS DADOS

            // Verifica se os dados enviados pelo formulário passaram pelas validações do CadastroUsuarioViewModel.

            // Exemplos:
            // - Nome obrigatório
            // - E-mail válido
            // - Senha com pelo menos 6 caracteres
            // - Senhas iguais
            if (!ModelState.IsValid)
            {
                // Retorna os erros de validação para o JavaScript, que poderá apresentá-los visualmente no formulário.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Verifique os dados informados."
                });
            }


            // VERIFICAÇÃO DE E-MAIL

            // Verifica se já existe um usuário cadastrado utilizando o e-mail informado.
            if (_usuarioServices.EmailExiste(model.Email))
            {
                // Retorna uma resposta de erro informando especificamente o motivo.
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Este e-mail já está cadastrado."
                });
            }


            // CONVERSÃO DO VIEWMODEL PARA USUARIO


            // Cria uma nova entidade Usuario utilizando os dados recebidos e validados pelo formulário.

            // ConfirmarSenha não é copiado porque esse campo existe somente no ViewModel para validação.
            var usuario = new Usuario
            {
                Nome = model.Nome,
                Email = model.Email,
                Senha = model.Senha
            };


            // CADASTRO


            // Envia o usuário para o UsuarioServices.

            // O Service será responsável por:
            // - Normalizar o e-mail
            // - Gerar o hash da senha
            // - Definir o usuário como Cliente
            // - Salvar o usuário no banco
            _usuarioServices.Cadastrar(usuario);


            // RESPOSTA DE SUCESSO

            // Retorna uma resposta que poderá ser interpretada pelo JavaScript do formulário.
            return Ok(new
            {
                sucesso = true,
                mensagem = "Conta criada com sucesso!"
            });
        }
    }
}