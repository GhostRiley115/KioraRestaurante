# Comentários preservados na integração mobile

A integração preserva os comentários anteriores do projeto, inclusive aqueles que a main já havia reescrito. Este registro é histórico; os contratos atuais estão em API-MOBILE.md.

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //Localiza o usuário pelo Id e verifica se a conta ainda está ativa.
```

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //Caso o usuário não seja encontrado ou já esteja desativado,
```

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //a operação não será realizada.
```

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //Desativa a conta logicamente.
```

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //O registro do usuário continuará existindo no banco.
```

Origem: `KioraRestaurante/Services/UsuarioService.cs`, versão `baa4dc4`.

```text
            //Salva a alteração no banco de dados.
```

Origem: `KioraRestaurante/DTOs/Usuario/UsuarioCadastroDTO.cs`, versão `baa4dc4`.

```text
        // ACEITE DOS TERMOS
```

Origem: `KioraRestaurante/DTOs/Usuario/UsuarioCadastroDTO.cs`, versão `baa4dc4`.

```text
        // Recebe a confirmação de aceite dos Termos de Uso
```

Origem: `KioraRestaurante/DTOs/Usuario/UsuarioCadastroDTO.cs`, versão `baa4dc4`.

```text
        // e da Política de Privacidade enviada pelo cadastro.
```

Origem: `KioraRestaurante/Controllers/ContaController.cs`, versão `baa4dc4`.

```text
            // Caso o usuário não seja encontrado ou já esteja desativado.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Evita repetir essa conexão ao configurar novos contextos.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Os contextos reutilizam a informação já descoberta.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// AUTENTICAÇÃO POR COOKIE
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Configura o sistema de autenticação do ASP.NET Core.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// O Cookie será utilizado para manter o usuário autenticado
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// depois que ele realizar o login.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Define para onde o usuário será enviado
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // caso tente acessar uma área protegida sem estar logado.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Define o caminho utilizado para sair da conta.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Clientes que tentam acessar a administração recebem uma página explicativa.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Define o tempo de validade do Cookie.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Renova automaticamente o Cookie enquanto o usuário
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // continuar utilizando o sistema.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Confere se a conta continua ativa em cada requisição autenticada.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Adiciona suporte aos Controllers e às Views do ASP.NET Core MVC.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// INJEÇÃO DE DEPENDÊNCIA
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Registra o UsuarioService para que o ASP.NET Core
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// possa fornecer automaticamente uma instância dele
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// para os Controllers que precisarem do serviço.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Limita o login por e-mail e, opcionalmente, por IP antes de executar a autenticação.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Envio e conferência de códigos têm limites próprios, sem consumir as tentativas de login.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// O endereço enviado ao geocodificador não deve aparecer nos logs de requisições HTTP.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Cache em memória para consultas públicas de CEP.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// HttpClient gerenciado pelo ASP.NET.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// RESERVAS: relógio injetável permite testar atrasos e meia-noite sem mudar a máquina.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Verifica se a aplicação não está em ambiente de desenvolvimento.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Utiliza uma página de erro personalizada.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
        // Ativa o HSTS para aumentar a segurança da aplicação.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Redireciona requisições HTTP para HTTPS.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Permite servir arquivos da pasta wwwroot,
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// omo CSS, JavaScript e imagens.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Ativa o sistema de roteamento.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Verifica o Cookie de autenticação e identifica
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// se existe um usuário conectado.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// verificar se o usuário possui autorização
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// para acessar determinadas áreas da aplicação.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Mapeia os arquivos estáticos da aplicação.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Compatibilidade com endereços antigos, sem gerar novos links com Account.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// A rota aponta para o mesmo controller e preserva os métodos GET e POST.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// Define a rota padrão da aplicação.
```

Origem: `KioraRestaurante/Program.cs`, versão `baa4dc4`.

```text
// INICIA A APLICAÇÃO
```
