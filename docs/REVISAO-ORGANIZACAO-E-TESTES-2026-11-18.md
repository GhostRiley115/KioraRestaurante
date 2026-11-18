# Revisão de organização e testes — 18/11/2026

A revisão foi feita em duas etapas: primeiro a organização do projeto, depois os testes e as correções. A base foi o commit `8bb35ec`, da branch `Clayton`. Os comentários existentes foram preservados. O guia [Arquitetura e organização](ARQUITETURA-E-ORGANIZACAO.md) explica a estrutura final e como acrescentar funcionalidades.

## Organização aplicada

Foram realocados 100 arquivos: 41 serviços, interfaces e utilidades; 26 controllers, entidades e modelos de tela; e 33 arquivos de testes. As referências C# e Razor foram atualizadas, incluindo os namespaces. Os arquivos JavaScript continuam na mesma pasta, preservando a resolução dos caminhos dos testes.

- `Services` passou a agrupar arquivos por funcionalidade. Interfaces e exceções ficam ao lado da implementação correspondente. Por exemplo: `Services/Produtos/ProdutoService.cs`, `IProdutoService.cs` e `RegraProdutoException.cs`.
- `Configuracao` reúne as extensões de inicialização de autenticação, documentação da API, respostas de erro e limites de requisições.
- `Helpers` reúne `HorarioRestaurante`, `ImagensHome` e `StatusVisual`.
- `Controllers/Api` reúne os controllers dedicados à API; `Controllers/Integracoes` recebe o webhook do Mercado Pago. Nomes de ações e rotas foram preservados, inclusive o login legado da equipe em `ContaController`.
- `ViewModels` ganhou pastas de conta, cardápio, produtos, pedidos, endereços, reservas e salão. O `ErrorViewModel` saiu de `Models` e passou para `ViewModels`.
- `EnderecoUsuario` saiu de `DTOs/Usuario` e passou para `Models`. Ele já usava o namespace de entidades; o mapeamento do banco permaneceu igual.
- Os testes C# foram agrupados por assunto. Seus namespaces compartilhados foram mantidos para preservar as fixtures existentes.
- Foram retiradas do arquivo do projeto quatro exclusões antigas que apontavam para a pasta inexistente `Services/Interface`.
- O README passou a refletir os recursos já existentes: pagamentos online, autenticação JWT da API, reservas, equipe, mesas e comandas.

O projeto continua sendo uma aplicação MVC única, com um projeto separado para testes. A organização não acrescenta uma camada de repositórios ou outros projetos. As Views, arquivos públicos, configurações privadas e migrations históricas foram preservados. Não há mudança de esquema de banco nesta revisão.

## Problemas encontrados e corrigidos

### Pagamentos online

Respostas do Mercado Pago com JSON válido, mas formato inesperado, ou data de atualização inválida podiam gerar erro 500. A integração agora recusa esses dados com uma falha controlada e mantém a situação anterior para nova consulta.

Valores monetários recebidos como booleano, objeto ou lista não são mais confundidos com valor ausente. Além disso, uma resposta `canceled` ou `failed` com valor recebido positivo passa para **Conferência**, com revisão financeira pendente. Ela não é registrada como cancelamento ou recusa sem cobrança. Os casos legítimos de aprovação, pendência, cancelamento sem cobrança e reenvio foram preservados.

Foram acrescentados 61 casos de teste de respostas e webhook. Eles verificam também assinatura válida e inválida, cabeçalhos inválidos, ausência de consulta ao provedor quando a assinatura é recusada e resposta de nova tentativa quando a gravação falha. O controller do webhook e a regra de assinatura já estavam corretos e não precisaram de alteração funcional.

### Imagens e histórico

A limpeza podia apagar uma imagem ainda referenciada quando sua URL Cloudinary não tinha extensão. O reconhecimento agora protege também esse formato, inclusive URLs com transformação, parâmetros e fragmentos. A verificação continua incluindo produtos inativos e as fotos históricas de delivery e comandas.

Foram acrescentados 37 casos para referências em uso, imagens realmente órfãs, identificadores fora da pasta gerenciada, simulação, limpeza desativada, novas tentativas, timeout e falhas de gravação no cadastro e na edição. A confirmação da nova referência e o agendamento da limpeza continuam juntos na gravação. Pequenos pontos de substituição do transporte permitem testar o upload e a exclusão sem acessar o Cloudinary.

### Salão, reservas e permissões

- Um grupo com prazo de espera vencido não pode mais ser acomodado antes de a manutenção atualizar a fila. Mesas já em atendimento continuam sem limite de permanência.
- Funcionários com convite pendente deixam de aparecer como responsáveis disponíveis para mesas.
- Listas de itens ou recebimentos contendo elementos nulos recebem erro de validação, evitando erro 500 e gravações parciais.
- A criação e o cancelamento de reservas reconferem a autorização depois de adquirir a trava compartilhada. Isso impede que uma conta desativada ou um gestor que perdeu o cargo conclua a operação enquanto aguardava outra alteração.

Essas situações receberam oito novos testes, incluindo requisições HTTP e revogação de acesso durante a operação. Os comentários e as regras de permanência, preparo, recebimento externo e liberação das mesas foram preservados.

## Resultados da validação

| Verificação | Resultado |
| --- | --- |
| Compilação integrada | Sem erros ou avisos |
| Testes .NET completos | **504 aprovados**, sem falhas ou casos ignorados |
| Novos casos .NET desta revisão | **106** |
| Testes JavaScript | **29 aprovados** |
| Verificação de sintaxe dos scripts próprios | **16 arquivos**, sem erros |
| Aplicação completa, iniciada pelo `Program` com MySQL isolado | **148 verificações** de páginas, arquivos e restrições de acesso, sem divergências |
| Criação de um banco MySQL vazio | **26 migrations aplicadas**, sem pendências |
| Nova aplicação das migrations | Concluída sem alterações adicionais |
| Modelo atual em relação ao snapshot | Sem alterações pendentes |
| Dependências diretas e transitivas | Nenhuma vulnerabilidade conhecida retornada pela consulta ao catálogo NuGet nesta revisão |

Os testes completos incluem cadastro e perfil, login e recuperação de senha, cookies e JWT, revogação de sessão, limites de tentativas, proteção antifalsificação, permissões por cargo, cardápio e combos, carrinho e concorrência, entrega e checkout, pagamentos, cupons, notificações, cancelamentos, reservas, equipe, salão, cozinha e relatórios.

A execução HTTP usou o `Program` real e os serviços periódicos, com banco próprio de teste. Isso complementa as fixtures SQLite, que não executam as migrations MySQL nem reproduzem toda a inicialização da aplicação.

### Fluxo presencial no MySQL

Também foi executado um fluxo completo usando os serviços reais e o banco temporário:

1. Acomodar o grupo e abrir a comanda.
2. Acrescentar duas rodadas, com três unidades ao todo.
3. Iniciar e concluir o preparo na cozinha; confirmar a entrega pelo garçom.
4. Aplicar desconto gerencial com motivo.
5. Fechar com registros divididos entre Pix e cartão.
6. Manter a mesa aguardando limpeza e liberá-la em outra operação.

O subtotal foi R$ 108,00, o desconto gerencial R$ 10,80 e o total R$ 97,20. O relatório físico confirmou esse valor e excluiu um pedido de delivery inserido no mesmo banco para a comparação. As duas notificações de prato pronto foram registradas.

### Conferência no navegador

Foram conferidos o carrinho de visitante, a transição para a conta do cliente, o checkout, a navegação administrativa e uma comanda ativa. As telas principais de administração, produtos, cupons, equipe, mesas, recepção, cozinha, configuração, cardápio e reservas foram verificadas em largura de 390 pixels, sem transbordamento horizontal da página. O painel foi conferido nos temas claro e escuro e também em largura de computador. Não houve erros ou avisos no console durante essa conferência.

## Limites e pontos para evolução

Esta validação usa contas fictícias e bancos temporários. Não grava no Aiven, não faz pagamentos ou estornos reais, não envia convites pelo Brevo e não exclui imagens do Cloudinary real. As respostas dos provedores de pagamento, e-mail, localização, rotas e imagens foram simuladas nos testes que dependem desses serviços. Assim, a revisão não certifica disponibilidade, saldo, cotas ou credenciais atuais desses provedores no Render.

O aplicativo Kotlin não faz parte deste repositório. Foram testados os contratos e as permissões da API que ele consumirá; a interface e a execução no dispositivo Android precisam de validação no projeto mobile.

A separação atual é adequada para continuar o TCC. `AppDbContext` e os arquivos que reúnem entidades relacionadas podem ser divididos por domínio no futuro, se o crescimento dificultar a leitura. Essa divisão não é necessária para o funcionamento atual e deve preservar o histórico de migrations. Os limites de tentativas ainda são mantidos por instância; uma futura hospedagem com várias instâncias exigirá contadores compartilhados.

Os testes locais que já existiam fora do Git foram preservados. Os dois arquivos C# foram realocados com os demais testes; o arquivo de tema JavaScript e os documentos locais anteriores foram mantidos.

## Como repetir os testes

Na raiz do repositório:

```bash
dotnet test KioraRestaurante.slnx
node --test KioraRestaurante.Tests/JavaScript/*.test.cjs
dotnet list KioraRestaurante.slnx package --vulnerable --include-transitive
```

A verificação de migrations deve ser repetida em um banco próprio de teste. Não use o banco compartilhado para recriar o esquema. Os resultados de navegador e de MySQL descritos acima são verificações complementares desta revisão; eles não são executados automaticamente pelos dois primeiros comandos.
