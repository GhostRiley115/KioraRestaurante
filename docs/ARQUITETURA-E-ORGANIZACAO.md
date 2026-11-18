# Arquitetura e organização do Kiora

Este documento explica onde cada parte do projeto fica e como continuar o desenvolvimento. A organização separa os assuntos do restaurante sem acrescentar projetos, camadas ou padrões desnecessários para o TCC.

## Um projeto MVC, duas formas de acesso

O Kiora continua sendo uma aplicação ASP.NET Core MVC única, com um projeto separado para testes. O site usa controllers e páginas Razor. O aplicativo Android pode consumir controllers de API que devolvem JSON. Os dois acessos usam os mesmos serviços quando a regra de negócio é a mesma.

```text
KioraRestaurante/             aplicação web e API
  Controllers/               entradas HTTP do site
    Api/                     endpoints JSON
    Integracoes/             notificações recebidas de serviços externos
  Services/                  operações organizadas por funcionalidade
  Models/                    entidades e tipos do domínio
  DTOs/                      dados de entrada e saída das operações
  ViewModels/                dados e validações destinados às telas
  Views/                     páginas e componentes Razor
  Data/                      contexto do Entity Framework Core
  Migrations/                histórico do esquema do banco
  Configuracao/              configuração de autenticação, API e limites
  Options/                   classes de opções lidas da configuração
  Filters/                   filtros e tratamento de requisições
  Helpers/                   pequenas utilidades compartilhadas
  wwwroot/                   CSS, JavaScript e arquivos públicos
  Program.cs                 montagem e inicialização da aplicação
KioraRestaurante.Tests/      testes agrupados por assunto
docs/                       documentação do projeto
```

O controller recebe a requisição, verifica a entrada e chama um serviço. O serviço executa a operação, verifica permissões e regras pertinentes e usa o `AppDbContext` quando precisa consultar ou gravar dados. A resposta segue para uma View ou é devolvida como JSON.

Não foi criada uma camada de repositórios apenas para envolver o Entity Framework. Para o tamanho atual do projeto, o uso do contexto nos serviços mantém o fluxo mais direto de estudar e manter.

## Serviços por funcionalidade

Os arquivos relacionados ficam juntos, incluindo a interface quando ela existe. Por exemplo, `Services/Produtos/ProdutoService.cs` e `Services/Produtos/IProdutoService.cs` pertencem ao mesmo assunto. Assim, não é necessário procurar a implementação em uma pasta e seu contrato em uma lista geral de interfaces.

| Pasta em `Services` | Responsabilidade e exemplos |
| --- | --- |
| `Admin` | Consultas administrativas, relatórios e exportação de vendas: `AdminService`, `RelatoriosAdminService`, `CsvVendasAdmin`. |
| `Autenticacao` | Emissão de JWT, validação de sessões e contagem de tentativas: `JwtService`, `ValidarSessaoUsuario`, `LimitadorLogin`. |
| `Cardapio` | Consulta do catálogo, coleções e ranking dos mais pedidos: `ConsultaCardapio`, `MaisPedidosService`. |
| `Carrinhos` | Carrinho da conta e do visitante, cookies e limpeza: `CarrinhoService`, `CarrinhoCookie`, `LimpezaCarrinhosService`. |
| `Cupons` | Validação, reserva, confirmação e acompanhamento do uso dos cupons. |
| `Email` | Envio de e-mails pelo provedor configurado: `EmailService` e `IEmailService`. |
| `Enderecos` | Consulta de CEP e manutenção dos endereços salvos pelo cliente. |
| `Entrega` | Localização do destino, cálculo da rota, distância, duração e cotação do frete. |
| `Imagens` | Upload, identificação e limpeza das imagens de produtos. |
| `Notificacoes` | Consulta, leitura e distribuição das notificações internas. |
| `PagamentosOnline` | Integração de pagamento com Mercado Pago, assinatura e conferência das operações. |
| `Pedidos` | Checkout e operações do delivery: `PedidoService`, `GerenciarPedidoService`, `AndamentoPedidoService`. |
| `Produtos` | Gestão de produtos, categorias, composição de combos e cálculo do preço de venda. |
| `Reservas` | Registro de reservas, horários, identificação do cliente e tratamento das reservas pendentes. |
| `Salao` | Atendimento presencial, comandas, cozinha, portaria, equipe e permissões de operação. |
| `Usuarios` | Cadastro, perfil, senha e demais operações da conta. |

`Cardapio` concentra a consulta e apresentação do catálogo; `Produtos` concentra sua manutenção e regras de venda. Eles continuam podendo compartilhar a mesma entidade `Produto`.

Exceções específicas também ficam na funcionalidade correspondente: `RegraProdutoException` em `Produtos`, `RegraPedidoException` em `Pedidos` e `RegraReservaException` em `Reservas`. Isso evita uma pasta geral de exceções desconectada do assunto a que cada regra pertence.

Não é necessário criar uma interface para toda classe. As interfaces existentes continuam onde são usadas como contrato ou substituição de implementação. Pequenos resultados e tipos auxiliares exclusivos de um serviço podem permanecer próximos dele; a divisão por pastas não exige um arquivo novo para cada tipo.

## Configuração, opções, filtros e utilidades

Essas pastas têm finalidades diferentes:

| Pasta | O que deve ficar nela |
| --- | --- |
| `Configuracao` | Extensões que montam a aplicação: `AutenticacaoKioraExtensions`, `ComportamentoApiExtensions`, `DocumentacaoApiExtensions` e `LimitacaoRequisicoesExtensions`. Elas são chamadas durante a inicialização; não representam operações de um pedido ou produto. |
| `Options` | Estruturas que recebem valores de configuração, como `JwtOptions`, `EntregaOptions`, `ReservaOptions` e `MercadoPagoConfig`. As chaves secretas continuam fora do código versionado. |
| `Filters` | Comportamento aplicado às requisições, como limitação de tentativas, proteção antifalsificação e tratamento de erros da API. |
| `Helpers` | Utilidades pequenas: `StatusVisual` escolhe classes visuais de status; `ImagensHome` monta URLs públicas das imagens; `HorarioRestaurante` converte os horários UTC para o horário local usado nas telas e filtros. |

`Helpers` não deve virar um depósito de regras de negócio. Se uma classe consulta o banco, gerencia uma operação ou pertence claramente a um assunto, o lugar adequado tende a ser `Services/<Funcionalidade>`.

## Models, DTOs e ViewModels

Esses nomes indicam o papel dos dados, e não apenas uma preferência de pasta.

| Tipo | Para que serve | Exemplo no projeto |
| --- | --- | --- |
| Model | Representa um conceito do domínio. As entidades persistidas têm seu mapeamento no Entity Framework. | `Produto`, `Pedido`, `EnderecoUsuario`, `AtendimentoMesa`. |
| DTO | Define os dados transferidos por uma operação, com os campos necessários para entrada ou resposta. Não representa automaticamente uma tabela. | `CriarPedidoRequestDTO`, `ProdutoResponseDTO`, `UsuarioCadastroDTO`. |
| ViewModel | Reúne os dados que uma tela precisa, incluindo campos de formulário, filtros e validações da interface. | `CheckoutViewModel`, `CadastrarProdutoViewModel`, `ErrorViewModel`. |

O `EnderecoUsuario` estava fisicamente dentro de `DTOs/Usuario`, mas já era uma entidade no namespace `KioraRestaurante.Models`. Seu arquivo passou para `Models/EnderecoUsuario.cs`, conservando esse namespace e o mapeamento existente.

O `ErrorViewModel`, que fornece os dados da tela de erro, passou de `Models` para `ViewModels`. Ele não é uma entidade do banco.

Os namespaces das entidades persistidas e o histórico de migrations foram preservados. Reorganizar serviços não exige uma migration. Alterar o nome completo de uma entidade pode afetar a identificação do modelo pelo Entity Framework; por isso, uma reorganização futura de entidades precisa conferir o mapeamento antes de gerar qualquer alteração no banco. Migrations já aplicadas não devem ser reescritas apenas para acompanhar pastas novas.

## Controllers e Views

Os controllers MVC permanecem diretamente em `Controllers`. Os controllers dedicados à API estão em `Controllers/Api`, incluindo usuários, reservas, salão, equipe, cardápio, produtos administrativos e consulta de CEP. A rota legada `/api/equipe/login` continua em `ContaController` para manter a compatibilidade; alguns controllers MVC também devolvem JSON para os scripts do site. O recebimento de notificações do Mercado Pago fica em `Controllers/Integracoes/MpOrdersWebhookController.cs`.

Essa separação facilita localizar como a aplicação recebe cada tipo de acesso. Uma pasta de API não significa que todos os seus endpoints sejam destinados ao aplicativo do cliente: alguns atendem à administração ou são usados pela própria interface web.

As Views mantêm os caminhos MVC existentes, como `Views/Pedido`, `Views/Admin` e `Views/Restaurante`. Renomear essas pastas sem necessidade afetaria a descoberta das páginas, os componentes compartilhados e os testes de renderização. As referências aos serviços e utilidades acompanham os novos namespaces, preservando as rotas e os caminhos das páginas.

## Delivery e restaurante físico

A organização mantém os dois fluxos distinguíveis:

- **Delivery:** carrinho, checkout, endereço de entrega, frete, cupom e pagamento online usam principalmente `Carrinhos`, `Pedidos`, `Entrega`, `Cupons` e `PagamentosOnline`.
- **Atendimento presencial:** chegada, acomodação, mesa ativa, comanda aberta, preparação, registro de pagamento externo e liberação da mesa usam principalmente `Reservas` e `Salao`.
- **Dados compartilhados:** contas, produtos, catálogo, notificações e consultas administrativas podem servir aos dois contextos, respeitando as permissões de cada operação.

O aplicativo do cliente consulta o cardápio e faz reservas, com cadastro e login opcionais para vincular as reservas ao histórico. Ele não ganha um fluxo de delivery por consumir a mesma aplicação. A equipe usa as operações presenciais de acordo com o cargo. Compartilhar servidor ou catálogo não torna um pedido do salão um pedido de entrega.

Na documentação dos relatórios e nas futuras consultas, o canal de venda deve permanecer explícito para permitir analisar o físico, o delivery ou uma comparação entre ambos.

## Testes agrupados por assunto

Os testes C# ficam em pastas como `Autenticacao`, `Cardapio`, `Carrinho`, `Pedidos`, `Entrega`, `Cupons`, `Reservas`, `Salao`, `Notificacoes`, `Administracao`, `Api` e `Interface`. Os testes de scripts continuam em `JavaScript`.

Essa organização aproxima o teste do assunto que ele verifica. Por exemplo, uma mudança na troca de endereço deve ser acompanhada pelos testes de `Pedidos` e `Entrega`; uma mudança na acomodação de um grupo, pelos de `Salao` e `Reservas`. Testes de API conferem o comportamento HTTP, enquanto os de interface podem conferir a renderização Razor ou os scripts do navegador.

Os comandos da raiz do repositório continuam sendo:

```bash
dotnet test KioraRestaurante.slnx
node --test KioraRestaurante.Tests/JavaScript/*.test.cjs
```

Esta documentação descreve a organização; os resultados da execução dos testes devem ser registrados separadamente durante a validação da versão.

## Como acrescentar uma funcionalidade

1. **Identifique o assunto.** Para uma nova regra de produto, use `Services/Produtos`; para uma operação presencial, avalie `Services/Salao`. Só abra uma nova pasta quando houver uma responsabilidade realmente diferente.
2. **Escolha os dados.** Use uma entidade em `Models` quando houver um conceito persistido; um DTO para a entrada ou saída da operação; um ViewModel para necessidades específicas de uma tela. Não envie uma entidade inteira ao aplicativo apenas por conveniência.
3. **Implemente o serviço.** Coloque-o na pasta do assunto, com namespace correspondente, por exemplo `KioraRestaurante.Services.Produtos`. Se uma interface for útil, mantenha-a ao lado da implementação.
4. **Registre as dependências.** Quando o serviço precisar ser obtido por injeção de dependência, registre-o no `Program.cs`, respeitando o tempo de vida adequado. Uma interface não precisa ter uma implementação diferente para web e mobile se a regra for a mesma.
5. **Adicione a entrada HTTP.** Use um controller MVC para uma tela ou `Controllers/Api` para JSON. Valide o contrato, mantenha as permissões no fluxo e delegue a operação ao serviço.
6. **Acrescente o teste correspondente.** Coloque-o na pasta do assunto em `KioraRestaurante.Tests`. Prefira verificar uma regra ou um comportamento importante, incluindo recusas de acesso e situações inválidas pertinentes à mudança.
7. **Atualize os contratos e a documentação.** Mudanças consumidas pelo Android devem aparecer na documentação da API. Uma migration só deve ser criada se o modelo persistido mudar; mover um arquivo de serviço não altera o banco.

Comentários existentes e documentos históricos continuam preservados. Documentos antigos podem citar caminhos anteriores como registro da época; para localizar os arquivos atuais, use o mapa deste documento e a estrutura do repositório.
