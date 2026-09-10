//Permite utilizar os DTOs
using KioraRestaurante.DTOs.Usuario;


namespace KioraRestaurante.Services.Interfaces
{
    /*Define as operações que podem ser realizadas
      relacionadas aos usuários do sistema.*/
    public interface IUsuarioService
    {
        /* ---- CADASTRO ---- */
        bool EmailExiste(string email);

        UsuarioResponseDTO Cadastrar(UsuarioCadastroDTO dto);

        /* ---- LOGIN ---- */
        UsuarioResponseDTO? Autenticar(UsuarioLoginDTO dto);

        /* ---- RECUPERAÇÃO DE SENHA ---- */
        //Gera um token para recuperação de senha.
        string? GerarTokenRecuperacao(UsuarioSolicitarRecuperacaoDTO dto);

        //Redefine a senha utilizando o token
        bool RedefinirSenha(UsuarioRedefinirSenhaDTO dto);

        /* ---- PERFIL ---- */
        UsuarioResponseDTO? BuscarPorId(int usuarioId);

        /*O Id do usuário atual é informado para que o próprio
        e-mail dele não seja considerado como duplicado.*/
        bool EmailExisteParaOutroUsuario(string email, int usuarioId);

        //Neste momento serão atualizados somente: Nome e e-mail
        bool AtualizarPerfil(int usuarioId, UsuarioAtualizarPerfilDTO dto);

        bool AlterarSenha(int usuarioId, UsuarioAlterarSenhaDTO dto);
    }
}