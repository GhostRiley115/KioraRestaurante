//Permite utilizar os DTOs
using KioraRestaurante.DTOs.Usuario;


namespace KioraRestaurante.Services.Interfaces
{
    /*Define as operações que podem ser realizadas
      relacionadas aos usuários do sistema.*/
    public interface IUsuarioService
    {
        //Cadastro
        bool EmailExiste(string email);

        UsuarioResponseDTO Cadastrar(UsuarioCadastroDTO dto);

        //Login
        UsuarioResponseDTO? Autenticar(UsuarioLoginDTO dto);

        //Recuperação de senha
        //Gera um token para recuperação de senha.
        string? GerarTokenRecuperacao(UsuarioSolicitarRecuperacaoDTO dto);

        //Redefine a senha utilizando o token
        bool RedefinirSenha(UsuarioRedefinirSenhaDTO dto);

        //Perfil
        /*O Id do usuário atual é informado para que o próprio
        e-mail dele não seja considerado como duplicado.*/
        bool EmailExisteParaOutroUsuario(string email, int usuarioId);

        /*Neste momento serão atualizados somente:
            - Nome
            - E-mail*/
        bool AtualizarPerfil(int usuarioId, UsuarioAtualizarPerfilDTO dto);

        bool AlterarSenha(int usuarioId, UsuarioAlterarSenhaDTO dto);
    }
}