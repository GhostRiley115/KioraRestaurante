using KioraRestaurante.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.DTOs.Usuario;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Interfaces;
/*Permite utilizar o PasswordHasher,
responsável por transformar e verificar senhas com hash.*/
using Microsoft.AspNetCore.Identity;

namespace KioraRestaurante.Services
{
    //A classe UsuarioService implementa a interface IUsuarioService.
    //Aqui ficará a lógica relacionada aos usuários
    public class UsuarioService : IUsuarioService
    {
        /*acesso ao banco de dados, através dele conseguimos:
        Buscar usuários, Adicionar usuários, Alterar usuários
        e Salvar alterações*/
        private readonly AppDbContext _context;

        /*Responsável por criar o hash da senha e verificar
        se uma senha informada corresponde ao hash armazenado.*/
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public UsuarioService(AppDbContext context)
        {
            /*Recebe o AppDbContext através da injeção de dependência
            e guarda na variável _context.*/
            _context = context;

            //Cria uma instância do PasswordHasher.
            _passwordHasher = new PasswordHasher<Usuario>();
        }

        private static string NormalizarEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        /*Converte a entidade Usuario em um UsuarioResponseDTO.
        Isso evita retornar dados internos ou sensíveis da entidade,
        como SenhaHash e informações de recuperação de senha.*/
        private static UsuarioResponseDTO ParaResponseDTO(Usuario usuario)
        {
            return new UsuarioResponseDTO
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Tipo = usuario.Tipo,
                Ativo = usuario.Ativo,
                VersaoSessao = usuario.VersaoSessao
            };
        }

        public UsuarioResponseDTO? BuscarPorId(int usuarioId)
        {
            var usuario = _context.Usuarios.AsNoTracking()
                .FirstOrDefault(u => u.Id == usuarioId && u.Ativo);

            if (usuario == null)
                return null;

            return ParaResponseDTO(usuario);
        }

        /* ---- CADASTRO ---- */
        //Verifica se já existe um usuário cadastrado
        public bool EmailExiste(string email)
        {
            email = NormalizarEmail(email);

            return _context.Usuarios.Any(u => u.Email == email);
        }

        /* ---- REALIZA O CADASTRO DE UM NOVO USUÁRIO. ---- */
        public UsuarioResponseDTO Cadastrar(UsuarioCadastroDTO dto)
        {
            Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);
            var usuario = new Usuario
            {
                Nome = dto.Nome.Trim(),
                Email = NormalizarEmail(dto.Email),
                Tipo = TipoUsuario.Cliente, //impede que escolha "Administrador" durante o cadastro.
                Ativo = true,
                AceitouTermos = dto.AceiteTermos
            };

            /*A senha digitada pelo usuário NÃO será armazenada diretamente no banco.
            O PasswordHasher cria um hash seguro da senha.*/
            usuario.SenhaHash = _passwordHasher.HashPassword(usuario, dto.Senha);

            //Adiciona o novo usuário ao contexto do Entity Framework.
            _context.Usuarios.Add(usuario);
            //Confirma a operação e salva o usuário no banco.
            _context.SaveChanges();
            //Retorna o usuário que acabou de ser cadastrado.
            return ParaResponseDTO(usuario);
        }

        /* ---- LOGIN ---- */
        public UsuarioResponseDTO? Autenticar(UsuarioLoginDTO dto)
        {
            var email = NormalizarEmail(dto.Email);

            /*Procura um usuário pelo e-mail. FirstOrDefault retorna:
            O usuário encontrado -> null caso não encontre*/
            var usuario = _context.Usuarios.AsNoTracking()
                .FirstOrDefault(u => u.Email == email && u.Ativo);

            //Se não encontrou nenhum usuário, o login não pode continuar.
            if (usuario == null)
                return null;

            //Compara a senha digitada pelo usuário com o hash armazenado no banco.
            var resultado = _passwordHasher.VerifyHashedPassword(
                usuario, usuario.SenhaHash, dto.Senha);

            //Se a senha estiver incorreta, retorna null.
            if (resultado == PasswordVerificationResult.Failed)
                return null;

            //Se a senha estiver correta, retorna o usuário autenticado e atualiza a senha Hash.
            if( resultado == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var senhaHash = _passwordHasher.HashPassword(usuario, dto.Senha);
                // Um login concorrente não pode sobrescrever uma senha recém-alterada.
                var atualizados = _context.Usuarios
                    .Where(u => u.Id == usuario.Id && u.Ativo && u.VersaoSessao == usuario.VersaoSessao
                        && u.SenhaHash == usuario.SenhaHash)
                    .ExecuteUpdate(set => set.SetProperty(u => u.SenhaHash, senhaHash));
                if (atualizados == 0)
                    return null;
            }
            //Retorna o usuário.
            return ParaResponseDTO(usuario);
        }

        /* ---- RECUPERAÇÃO DE SENHA ---- */
        //Gera um token para recuperação de senha.
        public string? GerarTokenRecuperacao(UsuarioSolicitarRecuperacaoDTO dto)
        {
            var email = NormalizarEmail(dto.Email);

            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Email == email && u.Ativo);
            if(usuario == null) 
                return null;

            //Gera um identificador único.
            var token = Guid.NewGuid().ToString();

            //Salva o token no usuário.
            usuario.TokenRecuperacaoSenha = token;
            /*Define por quanto tempo o token será válido.
            Neste caso: token criado agora + 30 minutos de validade.*/
            usuario.ExpiracaoTokenRecuperacaoSenha = DateTime.UtcNow.AddMinutes(30);

            //Salva o token e a data de expiração no banco.
            _context.SaveChanges();

            //Retorna o token para quem chamou o método.
            return token;
        }

        /* ---- REDEFINE A SENHA UTILIZANDO UM TOKEN VALIDO. ---- */
        public bool RedefinirSenha(UsuarioRedefinirSenhaDTO dto)
        {
            //Procura um usuário que possua exatamente o token informado.
            var usuario = _context.Usuarios.AsNoTracking()
                .FirstOrDefault(u => u.TokenRecuperacaoSenha == dto.Token && u.Ativo);

            //Se não encontrou usuário com esse token, a recuperação não pode continuar.
            if (usuario == null)
                return false;

            /*Verifica se existe uma data de expiração.
            Se não existir, o token não é considerado válido.*/
            if (usuario.ExpiracaoTokenRecuperacaoSenha == null)
                return false;

            /*Verifica se o token já passou do prazo. Se a data de expiração
            for menor que o horário atual, o token está expirado.*/
            if (usuario.ExpiracaoTokenRecuperacaoSenha < DateTime.UtcNow)
                return false;

            //Primeiro transformamos a nova senha em hash.
            var senhaHash = _passwordHasher.HashPassword(usuario, dto.NovaSenha);
            var novaVersao = Guid.NewGuid();

            //Depois que a senha foi alterada, o token não pode mais ser utilizado.
            // Salva todas as alterações no banco.
            // A condição atômica também impede reutilizar o token em duas requisições simultâneas.
            var atualizados = _context.Usuarios
                .Where(u => u.Id == usuario.Id && u.Ativo && u.VersaoSessao == usuario.VersaoSessao
                    && u.TokenRecuperacaoSenha == dto.Token
                    && u.ExpiracaoTokenRecuperacaoSenha >= DateTime.UtcNow)
                .ExecuteUpdate(set => set.SetProperty(u => u.SenhaHash, senhaHash)
                    .SetProperty(u => u.VersaoSessao, novaVersao)
                    .SetProperty(u => u.TokenRecuperacaoSenha, (string?)null)
                    .SetProperty(u => u.ExpiracaoTokenRecuperacaoSenha, (DateTime?)null));

            // Informa que a senha foi alterada com sucesso.
            return atualizados == 1;
        }

        /* ---- PERFIL ---- */
        /*O usuário atual é ignorado através do seu Id. Isso permite que o usuário 
        continue utilizando o próprio e-mail sem receber uma mensagem
        informando que o e-mail já está cadastrado.*/
        public bool EmailExisteParaOutroUsuario(string email, int usuarioId)
        {
            email = NormalizarEmail(email);

            /*Procura no banco um usuário que:
            1. Possua o mesmo e-mail informado.
            2. Possua um Id diferente do usuário atual.
            Se encontrar, significa que o e-mail já pertence a outro usuário.*/
            return _context.Usuarios
                .Any(u => u.Email == email && u.Id != usuarioId);
        }

        /* ---- ATUALIZAR PERFIL ---- */
        //Atualiza nome e e-mail.
        public bool AtualizarPerfil(int usuarioId, UsuarioAtualizarPerfilDTO dto)
        {
            Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);
            //Localiza o usuário existente no banco de dados através do id recebido.
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Id == usuarioId && u.Ativo);

            //Caso o usuário não seja encontrado, encerra a operação sem realizar alterações.
            if (usuario == null)
                return false;

            usuario.Nome = dto.Nome.Trim();
            usuario.Email = NormalizarEmail(dto.Email);

            _context.SaveChanges();

            return true;
        }

        /* ---- ALTERAÇÃO DE SENHA ---- */
        /*Altera a senha do usuário. Antes de salvar a nova senha, o sistema verifica se a senha atual informada está correta.
        A nova senha é armazenada utilizando o mesmo PasswordHasher de cadastro e login.*/
        public Guid? AlterarSenha(int usuarioId, UsuarioAlterarSenhaDTO dto, Guid versaoSessao)
        {
            var usuario = _context.Usuarios.AsNoTracking()
                .FirstOrDefault(u => u.Id == usuarioId && u.Ativo && u.VersaoSessao == versaoSessao);

            if (usuario == null || versaoSessao == Guid.Empty)
                return null;

            //O PasswordHasher compara a senha informada com o hash armazenado em usuario.SenhaHash.
            var resultado = _passwordHasher.VerifyHashedPassword(
                usuario, usuario.SenhaHash, dto.SenhaAtual);

            if (resultado == PasswordVerificationResult.Failed)
                return null;

            var senhaHash = _passwordHasher.HashPassword(usuario, dto.NovaSenha);
            var novaVersao = Guid.NewGuid();

            // Confirma a versão junto com a gravação: uma sessão já revogada não pode renovar o cookie.
            var atualizados = _context.Usuarios
                .Where(u => u.Id == usuarioId && u.Ativo && u.VersaoSessao == versaoSessao
                    && u.SenhaHash == usuario.SenhaHash)
                .ExecuteUpdate(set => set.SetProperty(u => u.SenhaHash, senhaHash)
                    .SetProperty(u => u.VersaoSessao, novaVersao)
                    .SetProperty(u => u.TokenRecuperacaoSenha, (string?)null)
                    .SetProperty(u => u.ExpiracaoTokenRecuperacaoSenha, (DateTime?)null));

            // Retorna exatamente a versão desta alteração, sem adotar a de outra requisição concorrente.
            return atualizados == 1 ? novaVersao : null;
        }

        /* ---- DESATIVAR CONTA ---- */
        //Desativa a conta sem excluir o usuário do banco de dados.
        public bool DesativarConta(int usuarioId)
        {
            //Localiza o usuário pelo Id e verifica se a conta ainda está ativa.
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Id == usuarioId && u.Ativo);

            //Caso o usuário não seja encontrado ou já esteja desativado,
            //a operação não será realizada.
            if (usuario == null)
                return false;

            //Desativa a conta logicamente.
            //O registro do usuário continuará existindo no banco.
            usuario.Ativo = false;
            usuario.VersaoSessao = Guid.NewGuid();
            usuario.TokenRecuperacaoSenha = null;
            usuario.ExpiracaoTokenRecuperacaoSenha = null;

            //Salva a alteração no banco de dados.
            _context.SaveChanges();

            return true;
        }
    }
}