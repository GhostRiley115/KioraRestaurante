// Permite acessar o AppDbContext,
// que é responsável pela comunicação com o banco de dados.
using KioraRestaurante.Data;

// Permite utilizar a classe Usuario.
using KioraRestaurante.Models;

// Permite utilizar a interface IUsuarioService.
using KioraRestaurante.Services.Interfaces;

// Permite utilizar o PasswordHasher,
// responsável por transformar e verificar senhas com hash.
using Microsoft.AspNetCore.Identity;

// Permite utilizar recursos do Entity Framework Core.
using Microsoft.EntityFrameworkCore;


namespace KioraRestaurante.Services
{
    // A classe UsuarioService implementa a interface IUsuarioService.
    //
    // Aqui ficará a lógica relacionada aos usuários:
    // - Cadastro
    // - Login
    // - Recuperação de senha
    public class UsuarioServices : IUsuarioServices
    {
        // Representa o acesso ao banco de dados.
        //
        // Através dele conseguimos:
        // - Buscar usuários
        // - Adicionar usuários
        // - Alterar usuários
        // - Salvar alterações
        private readonly AppDbContext _context;


        // Responsável por criar o hash da senha
        // e posteriormente verificar se uma senha informada
        // corresponde ao hash armazenado.
        private readonly PasswordHasher<Usuario> _passwordHasher;


        // CONSTRUTOR
        
        public UsuarioServices(AppDbContext context)
        {
            // Recebe o AppDbContext através da injeção de dependência
            // e guarda na variável _context.
            _context = context;


            // Cria uma instância do PasswordHasher.
            //
            // Ele será utilizado tanto no cadastro quanto
            // na alteração da senha.
            _passwordHasher = new PasswordHasher<Usuario>();
        }


        // CADASTRO

        // Verifica se já existe um usuário cadastrado
        // utilizando o e-mail informado.
        public bool EmailExiste(string email)
        {
            email = email.Trim().ToLower();

            return _context.Usuarios
                .Any(u => u.Email == email);
        }


        // Realiza o cadastro de um novo usuário.
        public Usuario Cadastrar(Usuario usuario)
        {
            // Remove espaços desnecessários no começo e no final e transforma o e-mail em letras minúsculas.
            
            // Exemplo: "  KARINA@EMAIL.COM  "
            
            // passa a ser: "karina@email.com"
            usuario.Email = usuario.Email.Trim().ToLower();


            // A senha digitada pelo usuário NÃO será armazenada diretamente no banco.
            
            // O PasswordHasher cria um hash seguro da senha.
            
            // Exemplo: senha digitada: "123456"
            
            // banco: "$2a$..." (exemplo ilustrativo)
            usuario.Senha = _passwordHasher.HashPassword(
                usuario,
                usuario.Senha
            );


            // Todo usuário criado pelo cadastro público será automaticamente um Cliente.
            
            // Isso impede que uma pessoa escolha "Administrador" durante o cadastro.
            usuario.Tipo = Models.Enums.TipoUsuario.Cliente;


            // Adiciona o novo usuário ao contexto do Entity Framework.
            _context.Usuarios.Add(usuario);


            // Confirma a operação e salva o usuário no banco.
            _context.SaveChanges();


            // Retorna o usuário que acabou de ser cadastrado.
            return usuario;
        }

               
        // LOGIN
        

        // Tenta autenticar um usuário utilizando e-mail e senha.
        
        // Se os dados estiverem corretos, retorna o usuário.
        //
        // Se estiverem incorretos, retorna null.
        public Usuario? Autenticar(string email, string senha)
        {
            // Remove espaços do e-mail e transforma todas as letras em minúsculas.
            email = email.Trim().ToLower();


            // Procura um usuário pelo e-mail.
            
            // FirstOrDefault retorna:
            // - O usuário encontrado
            // - null caso não encontre
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Email.ToLower() == email);


            // Se não encontrou nenhum usuário, o login não pode continuar.
            if (usuario == null)
                return null;


            // Compara a senha digitada pelo usuário com o hash armazenado no banco.
            //
            // Importante: não estamos comparando as strings diretamente.
            //
            // O PasswordHasher verifica se a senha informada corresponde ao hash salvo.
            var resultado = _passwordHasher.VerifyHashedPassword(
                usuario,
                usuario.Senha,
                senha
            );


            // Se a senha estiver correta, retorna o usuário autenticado.
            if (resultado == PasswordVerificationResult.Success)
                return usuario;


            // Se a senha estiver incorreta, retorna null.
            return null;
        }

           
        // RECUPERAÇÃO DE SENHA
        

        // Procura um usuário pelo e-mail.
        
        // Será utilizado no processo de "Esqueci minha senha".
        public Usuario? BuscarPorEmail(string email)
        {
            // Normaliza o e-mail antes da busca.
            email = email.Trim().ToLower();


            // Procura o usuário na tabela Usuarios.
            
            // Retorna:
            // - Usuário encontrado
            // - null caso não exista
            return _context.Usuarios
                .FirstOrDefault(u => u.Email.ToLower() == email);
        }


        // Gera um token para recuperação de senha.
        public string GerarTokenRecuperacao(Usuario usuario)
        {
            // Gera um identificador único.
            
            // Esse valor será utilizado temporariamente para identificar a solicitação de recuperação.
            string token = Guid.NewGuid().ToString();


            // Salva o token no usuário.
            usuario.TokenRecuperacaoSenha = token;


            // Define por quanto tempo o token será válido.
            
            // Neste caso: token criado agora + 30 minutos de validade.
            usuario.ExpiracaoTokenRecuperacaoSenha =
                DateTime.Now.AddMinutes(30);


            // Salva o token e a data de expiração no banco.
            _context.SaveChanges();


            // Retorna o token para quem chamou o método.
            return token;
        }


        // Redefine a senha utilizando um token válido.
        public bool RedefinirSenha(string token, string novaSenha)
        {
            // Procura um usuário que possua exatamente o token informado.
            var usuario = _context.Usuarios
                .FirstOrDefault(u =>
                    u.TokenRecuperacaoSenha == token);


            // Se não encontrou usuário com esse token, a recuperação não pode continuar.
            if (usuario == null)
                return false;


            // Verifica se existe uma data de expiração.
            
            // Se não existir, o token não é considerado válido.
            if (usuario.ExpiracaoTokenRecuperacaoSenha == null)
                return false;


            // Verifica se o token já passou do prazo.
            
            // Se a data de expiração for menor que o horário atual,
            // o token está expirado.
            if (usuario.ExpiracaoTokenRecuperacaoSenha < DateTime.Now)
                return false;


            // A nova senha também NÃO será salva em texto puro.
            
            // Primeiro transformamos a nova senha em hash.
            usuario.Senha = _passwordHasher.HashPassword(
                usuario,
                novaSenha
            );


            // Depois que a senha foi alterada, o token não pode mais ser utilizado.
            
            // Isso faz com que o token seja de uso único.
            usuario.TokenRecuperacaoSenha = null;


            // Remove também a data de expiração.
            usuario.ExpiracaoTokenRecuperacaoSenha = null;


            // Salva todas as alterações no banco.
            _context.SaveChanges();


            // Informa que a senha foi alterada com sucesso.
            return true;
        }

        // ================================================================
        // EDIÇÃO DO PERFIL
        // ================================================================

        // Verifica se o e-mail informado já pertence
        // a outro usuário cadastrado no banco de dados.
        //
        // O usuário atual é ignorado através do seu Id.
        //
        // Isso permite que o usuário continue utilizando
        // o próprio e-mail sem receber uma mensagem
        // informando que o e-mail já está cadastrado.
        public bool EmailExisteParaOutroUsuario(
            string email,
            int usuarioId
        )
        {
            // Remove espaços desnecessários do início e do final
            // do endereço de e-mail.
            email = email.Trim().ToLower();

            // Procura no banco um usuário que:
            //
            // 1. Possua o mesmo e-mail informado.
            // 2. Possua um Id diferente do usuário atual.
            //
            // Se encontrar, significa que o e-mail
            // já pertence a outro usuário.
            return _context.Usuarios
                .Any(u =>
                    u.Email.ToLower() == email &&
                    u.Id != usuarioId
                );
        }


        // ================================================================
        // ATUALIZAR PERFIL
        // ================================================================

        // Atualiza os dados permitidos do perfil
        // do usuário no banco de dados.
        //
        // Neste momento serão alterados somente:
        // - Nome
        // - E-mail
        //
        // A senha, o tipo de usuário, o carrinho,
        // os pedidos e os dados de recuperação de senha
        // não serão alterados por esta função.
        public void AtualizarPerfil(Usuario usuario)
        {
            // Localiza o usuário existente no banco de dados
            // através do Id recebido.
            var usuarioBanco = _context.Usuarios
                .FirstOrDefault(u => u.Id == usuario.Id);

            // Caso o usuário não seja encontrado,
            // encerra a operação sem realizar alterações.
            if (usuarioBanco == null)
                return;


            // ============================================================
            // ATUALIZAÇÃO DO NOME
            // ============================================================

            // Remove espaços desnecessários do nome
            // antes de salvar no banco de dados.
            usuarioBanco.Nome = usuario.Nome.Trim();


            // ============================================================
            // ATUALIZAÇÃO DO E-MAIL
            // ============================================================

            // Remove espaços desnecessários do e-mail
            // e transforma as letras em minúsculas.
            //
            // Isso mantém o mesmo padrão utilizado
            // no cadastro e no login.
            usuarioBanco.Email = usuario.Email.Trim().ToLower();


            // ============================================================
            // SALVAR ALTERAÇÕES
            // ============================================================

            // Envia as alterações realizadas para o banco de dados.
            _context.SaveChanges();
        }

        // ================================================================
        // ALTERAÇÃO DE SENHA
        // ================================================================

        // Altera a senha do usuário.
        //
        // Antes de salvar a nova senha, o sistema verifica
        // se a senha atual informada está correta.
        //
        // A nova senha será armazenada utilizando o mesmo
        // PasswordHasher utilizado no cadastro e no login.
        public bool AlterarSenha(
            int usuarioId,
            string senhaAtual,
            string novaSenha
        )
        {
            // ============================================================
            // BUSCAR USUÁRIO
            // ============================================================

            // Procura no banco de dados o usuário
            // através do seu Id.
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Id == usuarioId);


            // Verifica se o usuário foi encontrado.
            if (usuario == null)
            {
                // Caso o usuário não exista,
                // a alteração não poderá ser realizada.
                return false;
            }


            // ============================================================
            // VERIFICAR SENHA ATUAL
            // ============================================================

            // Verifica se a senha informada pelo usuário
            // corresponde à senha armazenada no banco.
            //
            // O PasswordHasher compara a senha informada
            // com o hash armazenado em usuario.Senha.
            var resultado = _passwordHasher.VerifyHashedPassword(
                usuario,
                usuario.Senha,
                senhaAtual
            );


            // Verifica se a senha atual está incorreta.
            if (resultado == PasswordVerificationResult.Failed)
            {
                // Impede a alteração da senha.
                return false;
            }


            // ============================================================
            // GERAR HASH DA NOVA SENHA
            // ============================================================

            // A nova senha nunca deve ser armazenada
            // diretamente no banco de dados.
            //
            // O PasswordHasher transforma a nova senha
            // em um hash seguro antes de armazená-la.
            usuario.Senha = _passwordHasher.HashPassword(
                usuario,
                novaSenha
            );


            // ============================================================
            // SALVAR ALTERAÇÃO
            // ============================================================

            // Envia a nova senha para o banco de dados.
            _context.SaveChanges();


            // ============================================================
            // RETORNO
            // ============================================================

            // Informa que a alteração foi realizada
            // com sucesso.
            return true;
        }
    }
}


