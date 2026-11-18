# Integração da main na Clayton e correções da API — 17/11/2026

A branch Clayton foi atualizada por avanço direto de `baa4dc4` para `db8e1f0`, incorporando o PR #55 da Karina que já estava na main. Os cinco arquivos locais não rastreados foram preservados. As correções abaixo foram desenvolvidas e testadas sobre essa versão e aplicadas ao mesmo projeto do Rider.

## Correções realizadas

- Login JSON e formulário compartilham o limite por conta, considerando normalização e collation. O cadastro JSON também ganhou limite por conexão.
- A configuração JWT é validada no início e usa a mesma chave, emissor, público e prazo na emissão e na validação. A chave local foi preparada nos User Secrets, fora do código.
- JWT é aceito somente pelas APIs. O delivery MVC continua exigindo o cookie do site. Cliente do aplicativo pode consultar, cadastrar/entrar opcionalmente e reservar; não recebe acesso ao checkout web pelo token.
- Bearer válido permite operações autenticadas sem cookies/CSRF. Bearer inválido não dispensa proteção nem recorre a um cookie como alternativa. Visitantes e acessos por cookie continuam com CSRF.
- Troca de senha preserva a versão exata daquela alteração e não adota outra revogação concorrente.
- Logout mobile revoga os acessos anteriores da conta; logout somente por cookie mantém o comportamento local do site. O guia explica essa diferença.
- Autodesativação e gestão do salão compartilham a trava do banco. Funcionário com mesa pendente não se desativa; dois administradores simultâneos não conseguem eliminar o último administrador ativo. A versão da requisição também é conferida.
- PATCHs de produto e desconto rejeitam campos omitidos; `false` e zero explícitos continuam funcionando. Erros esperados de cadastro de produto retornam 400 JSON e conflitos de gravação retornam 409.
- O alias de habilitação de mesa exige valor, versão e chave da solicitação. Reutiliza as regras do salão, preserva nome/capacidade, registra histórico e rejeita revisão antiga. O reenvio da mesma solicitação não duplica movimentos.
- Reservas públicas e formulários equivalentes compartilham cotas de consulta/escrita, sem depender apenas da chave anônima que o visitante pode trocar.
- O catálogo ganhou filtros equivalentes aos do site, coleções, composição de combos e consulta por ID sem carregar o catálogo inteiro. A resposta em lista foi preservada.
- A cozinha aceita filtro de origem para o aplicativo físico, mantendo a fila compartilhada do site e sem misturar comandas com pedidos de delivery.
- As respostas de erro das APIs são JSON, com mensagens seguras e código de suporte quando necessário. Swagger identifica consultas públicas e operações protegidas e continua restrito ao desenvolvimento.
- O prazo de reservas encerradas já fica oculto no HTML inicial. A confirmação de cancelamento acompanha o estado atual, evita envio repetido e tem alternativa quando Bootstrap não estiver disponível.

## Organização

O registro de autenticação, limites, documentação e respostas de API foi extraído de `Program.cs` para extensões pequenas e compartilhadas com os testes HTTP. Os comentários originais foram movidos junto e preservados. Outros 51 comentários que a main já havia reescrito foram guardados no [registro histórico da integração](COMENTARIOS-PRESERVADOS-INTEGRACAO-MOBILE.md). Os serviços do salão, pagamento, notificações e as integrações externas continuam registrados.

O guia [API mobile](API-MOBILE.md) explica os contratos de cliente/visitante, configuração, filtros, logout e códigos de erro. O documento [API da equipe e do salão](API-EQUIPE-E-SALAO.md) foi atualizado para refletir JWT e o alias versionado da mesa.

## MySQL

Foi atualizado o **conector MySqlConnector de 2.4.0 para 2.6.1**, com referência explícita no projeto, compatível com Pomelo 9. A auditoria de dependências não apontou vulnerabilidades conhecidas após a atualização. A conexão local existente com o Aiven passou em `SELECT 1`, com TLS ativo, sem alteração de dados.

O servidor MySQL do Aiven não foi atualizado ou substituído. Não foi criada migration e não houve alteração do esquema. A configuração TLS existente foi preservada; não foi desabilitada validação para acomodar a atualização.

## Verificação

Na cópia de trabalho da versão integrada, sem os testes que já existiam apenas no Rider, passaram **382 testes .NET**, incluindo **74 novos casos**, e **24 testes JavaScript**. As duas falhas anteriores de JavaScript foram resolvidas ao atualizar a simulação do novo modal e testar seus novos comportamentos. Na execução final do Rider, incluindo os testes locais preservados, passaram **398 testes .NET e 29 JavaScript**.

Os testes HTTP usam a configuração real da aplicação, com banco SQLite temporário e integrações externas bloqueadas. Eles cobrem reserva anônima, vínculo no cadastro/login, gravação com Bearer sem cookies, CSRF no acesso web, bloqueio de Bearer falso, permissões por cargo, impedimento de usar JWT no delivery MVC, cotas compartilhadas, logout/revogação, PATCH vazio/antigo, erros seguros e geração de Swagger. As provas de concorrência verificam a proteção do último administrador e a mudança de sessão durante alteração de senha.

Os testes locais adicionais que já existiam apenas no Rider foram preservados e são conferidos também na aplicação final. Pagamentos, reservas e contas reais não foram utilizados nos testes.

## Situação de publicação

Esta etapa atualiza o projeto local e a branch Clayton. Não envia automaticamente as correções ao GitHub nem ativa uma versão nova no Render. Antes da publicação, o serviço do Render precisa receber `Jwt__ChaveSecreta`; a chave não deve ser exposta no repositório, APK ou chat.

O novo contrato de logout encerra os acessos anteriores da conta. Não foi acrescentada uma tabela de dispositivos ou mecanismo de refresh token. O cliente deve apagar o token local e pedir novo login quando receber 401.
