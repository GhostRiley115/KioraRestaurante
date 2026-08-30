using KioraRestaurante.Models;

namespace KioraRestaurante.Services.Interfaces
{
    public interface IUsuarioServices
    {
        // Cadastro
        bool EmailExiste(string email); // Verifica se já existe uma conta com aquele e-mail. //
        Usuario Cadastrar(Usuario usuario); // Cadastra um novo usuário. //

        // Login
        Usuario? Autenticar(string email, string senha); // Verifica e-mail e senha e retorna o usuário se os dados forem válidos. //

                                                         // O? significa que pode retornar null caso o login seja inválido. //

        // Recuperação de senha
        Usuario? BuscarPorEmail(string email); // Procura o usuário pelo e - mail informado. //
        string GerarTokenRecuperacao(Usuario usuario); // Gera o token que será usado para recuperação. //
        bool RedefinirSenha(string token, string novaSenha); // Valida o token e permite definir uma nova senha. //
    }
}