# API do aplicativo Kiora

## Escopo e configuração

O aplicativo atende ao restaurante físico. O cliente pode consultar o cardápio e reservar sem conta; cadastro/login são opcionais para manter o histórico da reserva. Não há compra de delivery no app. Garçom, portaria, cozinha, gerente e administrador usam as permissões do salão já existentes.

Use HTTPS no endereço publicado. Nos exemplos, os valores entre `<...>` são substituídos pelos dados reais da consulta; não envie esses textos literalmente. Não embuta chaves de serviços no aplicativo.

O backend exige `Jwt:ChaveSecreta` com pelo menos 32 bytes. A chave local fica nos User Secrets do projeto; no Render, a variável equivalente é `Jwt__ChaveSecreta`. Os demais parâmetros têm estes padrões:

| Configuração | Padrão |
|---|---|
| `Jwt:Emissor` | `KioraRestauranteAPI` |
| `Jwt:Publico` | `AppKiora` |
| `Jwt:ValidadeMinutos` | `120`, aceitando de 5 a 120 |

A configuração é validada na inicialização; não se deve publicar sem preparar a chave no serviço. A chave de assinatura permanece no servidor e não é o token recebido pelo usuário. Não copie valores de teste para produção. As chaves dos ambientes podem ser diferentes.

No Rider, iniciar pelo perfil HTTPS continua sendo a opção local preferida. Os perfis `http`/`http-rede` existentes podem ser usados em uma rede de desenvolvimento controlada. No emulador Android, o host normalmente é acessado por `10.0.2.2`; em aparelho físico, use o endereço da máquina na rede. HTTP de desenvolvimento não deve virar configuração da versão publicada.

## Dois modos de acesso, sem retirar a segurança do site

| Acesso | Como autenticar | Proteção das gravações |
|---|---|---|
| Visitante | Sem conta; chave de visitante e cookies da sessão | `X-Reserva-Chave`, `CookieJar` e `X-CSRF-TOKEN` |
| App conectado | JWT retornado pelo login JSON | `Authorization: Bearer <token>` válido; não exige cookies ou CSRF |
| Site/cliente legado | Cookie de autenticação | Cookie e token CSRF válidos, inclusive pelas APIs antigas |

A API não confia na simples presença do cabeçalho Bearer. Assinatura, prazo, conta ativa, convite pendente, cargo e versão da sessão são verificados. Nas operações protegidas e nas gravações de reserva, um Bearer inválido retorna 401 e não usa um cookie válido como alternativa para contornar a proteção. O catálogo continua público.

Nas rotas MVC, como `/Pedido/Checkout`, o JWT não autentica a chamada. Essas páginas continuam usando a sessão web; isso impede transformar o token do aplicativo em acesso ao checkout do delivery.

## Consulta pública do cardápio

Não é necessário entrar:

- `GET /api/cardapio/categorias`
- `GET /api/cardapio/produtos`
- `GET /api/cardapio/produtos/{id}`

A lista de produtos mantém seu formato de array. Além de nome, descrição, categoria e imagem, o DTO retorna `preco` final, `precoOriginal`, `descontoPercentual`, `disponivel`, `ehCombo` e `composicao` opcional. A composição é um texto de apresentação; o garçom continua lançando o ID do produto/ combo, sem recalcular o preço no aplicativo.

Filtros opcionais de `/api/cardapio/produtos`:

| Parâmetro | Exemplo/valores |
|---|---|
| `categoriaId` | ID recebido de categorias; não combinar com coleção |
| `busca` | Parte do nome, até 100 caracteres |
| `precoMinimo`, `precoMaximo` | `25.90` ou `25,90`, sem separador de milhar |
| `ordenacao` | `padrao`, `menor-preco`, `maior-preco`, `nome-az`, `nome-za` |
| `colecao` | `mais-pedidos`, `combos`, `promocoes` |
| `somenteDisponiveis` | `true` ou `false` |
| `somentePromocoes` | `true` seleciona produtos com desconto; a coleção `promocoes` reúne combos ou produtos com desconto |

Exemplo: `/api/cardapio/produtos?colecao=combos&ordenacao=menor-preco`.

Filtros inválidos retornam 400 em vez de serem ignorados. Na ordem padrão, a lista segue as seções do site. A coleção de mais pedidos compartilha a janela quinzenal do sistema.

## Reserva sem conta

1. Crie um cliente HTTP com armazenamento de cookies (`CookieJar`).
2. Chame `GET /api/reservas/sessao` sem Authorization. Guarde `chaveVisitante`, `csrfToken`, `hoje`, `ultimaData` e `fuso`.
3. Consulte `GET /api/reservas/horarios?data=<yyyy-MM-dd>&pessoas=2`.
4. Para criar, envie `POST /api/reservas`, JSON, mantendo os cookies e os cabeçalhos `X-Reserva-Chave: <chaveVisitante>` e `X-CSRF-TOKEN: <csrfToken>`.

```json
{
  "nome": "Nome do grupo",
  "telefone": "11900000000",
  "pessoas": 2,
  "data": "2026-11-20",
  "horario": "19:00",
  "chaveSolicitacao": "UUID-NOVO-DA-SOLICITACAO"
}
```

Escolha data e horário válidos retornados pela API, em vez de copiar os valores ilustrativos. Pessoas: de 1 a 6. Telefone: 10 ou 11 dígitos com DDD. Gere um UUID real para a solicitação. Em timeout, repita a mesma chave; não gere uma nova ação sem consultar o resultado.

`GET /api/reservas/minhas` lista as reservas da identidade atual; `GET /api/reservas/{codigo}` consulta uma reserva própria. O aplicativo deve persistir a chave de visitante com cuidado, sem colocá-la em URLs, logs ou capturas. Perder a chave anônima pode impedir o acesso àquela reserva sem conta.

Para cancelar, envie `POST /api/reservas/cancelar` com `codigo` e a `versao` mais recente, usando a mesma identidade e proteção. O servidor valida se o estado ainda permite cancelar. A reserva aguarda chegada, sem prometer uma mesa específica nem impor limite de permanência ao atendimento iniciado.

## Cadastro opcional e login

`POST /api/usuarios/cadastro`, `Content-Type: application/json`:

```json
{
  "nome": "Nome do cliente",
  "email": "cliente@example.com",
  "senha": "SUA-SENHA",
  "confirmarSenha": "SUA-SENHA",
  "aceiteTermos": true
}
```

As regras de senha são as mesmas do site; o texto acima é apenas um marcador. Nome: até 100 caracteres. E-mail: até 150. O servidor sempre cadastra Cliente, independentemente de um cargo extra enviado no JSON. Sucesso: 201; e-mail duplicado/conflito: 409. Cadastro não faz login automaticamente: depois, use a rota de login.

`POST /api/usuarios/login`, JSON:

```json
{ "email": "cliente@example.com", "senha": "SUA-SENHA" }
```

Se o cliente já fez reservas sem conta, envie a mesma `X-Reserva-Chave` no cadastro/login. Somente as reservas anônimas daquela chave são vinculadas à conta. Após conectar, o histórico é consultado pelo JWT.

No sucesso, a resposta contém `token`, `tipoToken: "Bearer"` e `usuario`. O cargo vem em `usuario.tipo`: 0 Cliente, 1 Administrador, 2 Gerente, 3 Garçom, 4 Portaria, 5 Cozinha. A sessão da equipe apresenta nomes dos cargos como strings. Não use apenas o cargo salvo no Android para autorizar comandos: o servidor confere novamente.

Gravações autenticadas usam `Authorization: Bearer <token>` e JSON, sem precisar continuar enviando CSRF/cookies. Isso vale também para o cliente reservar conectado.

Rotas do usuário conectado:

| Método e rota | Uso |
|---|---|
| `GET /api/usuarios/perfil` | Perfil atual |
| `PUT /api/usuarios/perfil` | Nome e e-mail |
| `POST /api/usuarios/alterar-senha` | `senhaAtual`, `novaSenha`, `confirmarNovaSenha`; recebe token novo |
| `POST /api/usuarios/logout` | Revoga os acessos anteriores da conta |
| `DELETE /api/usuarios/conta` | Autodesativação, respeitando as regras da conta/equipe |

Ao alterar a senha, substitua o token local pelo retornado. As outras sessões são encerradas, e uma troca concorrente não é adotada silenciosamente. Um funcionário com mesa pendente não pode se desativar; é necessário transferir/liberar suas mesas. O último administrador ativo não pode se desativar, inclusive em requisições simultâneas.

## Logout, expiração e recuperação

**Nesta versão, sair pelo JWT revoga todos os acessos anteriores da conta**, incluindo tokens de outros aparelhos e cookies antigos do site. Essa escolha usa a versão de sessão já existente, sem acrescentar tabela de dispositivos ou refresh tokens. A resposta informa esse encerramento. Apague o token do Android e inicie uma nova identidade de visitante se continuar sem conta.

`POST /api/equipe/logout` também aceita Bearer e segue essa regra. Quando chamado somente com cookie e CSRF, mantém o logout local do acesso web.

Em 401, descarte a sessão inválida e peça login; não repita automaticamente alterações com outra chave. Os tokens expiram em até duas horas. Não há refresh token nesta etapa.

O fluxo de recuperação de senha do site continua disponível. O aplicativo pode abrir a experiência web segura para recuperação; não deve receber tokens/chaves do Brevo nem implementar envio de e-mail por conta própria. Se forem criadas telas nativas de recuperação, elas devem reutilizar os serviços e os limites existentes, com um contrato dedicado, sem armazenar senha de funcionário.

## Funcionários e separação do delivery

As operações, DTOs, versões, idempotência, recebimentos externos e permissões estão descritos em [API da equipe e do salão](API-EQUIPE-E-SALAO.md).

- Portaria consulta mesas/grupos e registra chegada/fila; não acomoda nem abre comanda.
- Garçom opera suas mesas; gestão pode intervir/transferir.
- Cozinha inicia preparo e marca pronto; garçom confirma entrega.
- Fechar a conta registra o recebimento externo e deixa a mesa aguardando limpeza; liberar a mesa é outra ação.
- Gerente e administrador consultam vendas físicas, descontos justificados e equipe conforme suas permissões.

Para o aplicativo físico, use `/api/salao/comandas/*` e `/api/salao/relatorio`. `/api/admin/pedidos` e `/api/admin/painel` consultam delivery e não devem alimentar comandas ou relatório físico.

`GET /api/salao/cozinha?origem=salao` retorna somente itens físicos; `origem=delivery` retorna delivery. Sem o filtro, a cozinha compartilhada do site continua vendo ambos. Cada item mantém sua origem e seu identificador próprio; o filtro não altera as regras de permissão.

## Validação, erros e repetição

| Código | Tratamento esperado no app |
|---|---|
| 400 | Exibir a mensagem/campos inválidos; não repetir silenciosamente |
| 401 | Pedir login ou reiniciar o fluxo de visitante sem token inválido |
| 403 | Não mostrar a operação para aquele cargo |
| 404 | Registro inexistente ou que não pertence ao usuário |
| 409 | Reconsultar o estado e pedir conferência antes de uma nova ação |
| 429 | Aguardar o prazo de `Retry-After` |
| 500 | Mensagem segura e código de suporte; consultar estado antes de repetir uma gravação |

As APIs retornam JSON, inclusive para erros inesperados; mensagens de validação podem incluir `erros` por campo. Senhas, chaves e exceções internas não são retornadas. Não interprete um 500 como garantia de que nenhuma gravação ocorreu.

PATCHs de situação/disponibilidade/desconto exigem o campo explícito. `{}` é inválido; `false` e zero continuam sendo valores legítimos. O alias de habilitação de mesa também exige `versao` e `chaveSolicitacao`.

O login JSON e o formulário compartilham até 5 tentativas por conta em um minuto. Reservas têm cotas de consulta e escrita compartilhadas entre API e formulário (120 consultas/minuto e 12 escritas/5 minutos por endereço de conexão). Cadastro JSON tem 6 tentativas/15 minutos por endereço. A aplicação não confia em `X-Forwarded-For` arbitrário. Em implantação com proxy, a configuração de proxy confiável deve ser feita antes de usar o IP encaminhado como identidade do cliente; sem isso, os limites por conexão podem ser compartilhados pelos usuários do proxy. Os limites são mantidos por instância, adequados à implantação atual; múltiplas instâncias exigem um armazenamento/dispositivo de limitação compartilhado.

## Documentação interativa e banco

Swagger está habilitado somente no ambiente Development. Ele distingue consultas públicas e operações que exigem Bearer. Nunca publique segredos como valores padrão da documentação.

O conector `MySqlConnector` foi fixado em 2.6.1 para retirar o alerta GHSA-473q-m89c-ghf8. Pomelo e o modelo do banco foram preservados; esta atualização não acrescenta migration nem muda o servidor MySQL do Aiven. A conexão existente foi conferida por leitura com TLS ativo. Não foi desabilitada validação TLS para acomodar o pacote.
