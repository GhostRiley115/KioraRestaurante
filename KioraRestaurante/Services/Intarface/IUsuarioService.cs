// Permite utilizar a classe Usuario,
// que representa o usuário salvo no banco de dados.
using KioraRestaurante.Models;


namespace KioraRestaurante.Services.Interfaces
{
    // ================================================================
    // INTERFACE DE SERVIÇOS DO USUÁRIO
    // ================================================================

    // Define as operações que podem ser realizadas
    // relacionadas aos usuários do sistema.
    //
    // A implementação dessas operações ficará na classe
    // UsuarioServices.
    public interface IUsuarioService
    {
        // ================================================================
        // CADASTRO
        // ================================================================

        // Verifica se determinado e-mail
        // já está cadastrado no banco de dados.
        bool EmailExiste(string email);

        // Cadastra um novo usuário no banco de dados.
        Usuario Cadastrar(Usuario usuario);


        // ================================================================
        // LOGIN
        // ================================================================

        // Procura o usuário pelo e-mail e verifica
        // se a senha informada está correta.
        Usuario? Autenticar(string email, string senha);


        // ================================================================
        // RECUPERAÇÃO DE SENHA
        // ================================================================

        // Procura um usuário pelo endereço de e-mail.
        Usuario? BuscarPorEmail(string email);

        // Gera um token para recuperação de senha.
        string GerarTokenRecuperacao(Usuario usuario);

        // Redefine a senha utilizando o token
        // de recuperação informado.
        bool RedefinirSenha(string token, string novaSenha);


        // ================================================================
        // EDIÇÃO DO PERFIL
        // ================================================================

        // Verifica se o e-mail informado já pertence
        // a outro usuário.
        //
        // O Id do usuário atual é informado para que
        // o próprio e-mail dele não seja considerado
        // como duplicado.
        bool EmailExisteParaOutroUsuario(
            string email,
            int usuarioId
        );

        // Atualiza os dados permitidos do perfil
        // do usuário no banco de dados.
        //
        // Neste momento serão atualizados somente:
        // - Nome
        // - E-mail
        void AtualizarPerfil(Usuario usuario);

        // ================================================================
        // ALTERAÇÃO DE SENHA
        // ================================================================

        // Altera a senha do usuário.
        //
        // Antes de realizar a alteração, o Service irá verificar
        // se a senha atual informada pelo usuário está correta.
        //
        // O método retorna:
        //
        // true  -> quando a senha foi alterada com sucesso.
        // false -> quando a senha atual estiver incorreta
        //          ou o usuário não for encontrado.
        bool AlterarSenha(
            int usuarioId,
            string senhaAtual,
            string novaSenha
        );
    }


}