# API da equipe e do salão

A API usa a autenticação por cookie existente. Não foi acrescentado JWT. O cliente Android deve manter os cookies numa `CookieJar`, usar HTTPS, obter o token CSRF e enviar o cabeçalho `X-CSRF-TOKEN` em operações de escrita. Não coloque chaves Brevo, Cloudinary, mapas ou Mercado Pago no aplicativo.

## Sessão e login

1. `GET /api/equipe/sessao` antes do login: retorna `csrfToken`, `usuario: null` e o fuso.
2. `POST /api/equipe/login`, com **formulário**, não JSON: `Email`, `Senha` e o cabeçalho CSRF obtido no passo anterior. Esse login usa o mesmo limite de tentativas do site.
3. Após receber sucesso, consulte novamente `GET /api/equipe/sessao`. A identificação mudou e o aplicativo precisa usar o novo `csrfToken` retornado.
4. A sessão retorna apenas `usuario.id`, `nome`, `cargo` e `gestor`, além do token CSRF e do fuso. Os cargos nesse retorno são strings: `Administrador`, `Gerente`, `Garcom`, `Portaria`, `Cozinha`.
5. `POST /api/equipe/logout` encerra a sessão atual.

Cookies vencidos, revogados ou de contas desativadas não dão acesso à API. Uma resposta 401 pede novo login. Cargo sem autorização retorna 403. O site mantém seu redirecionamento normal para a tela de login.

## Convenções dos comandos

- O corpo das operações do salão é **JSON**. Valores decimais no JSON usam ponto: `39.90`. Nos formulários web, vírgula e ponto são aceitos.
- Gere um UUID em `chaveSolicitacao` para cada nova ação. Em timeout, preserve exatamente a chave e o corpo da tentativa anterior. A mesma chave não pode ser reaproveitada para outra ação ou outro conteúdo.
- Sempre use a `versao` recebida na consulta que está sendo exibida. Após gravar, consulte novamente a comanda, mesa ou fila. Uma resposta 409 significa que os dados precisam ser conferidos antes de uma nova ação.
- Datas e horários de respostas do salão são UTC, com sufixo `Z`. Exiba no fuso `America/Sao_Paulo`. Os filtros `dia`, `comparar` e `data` usam `yyyy-MM-dd`.
- Os endpoints retornam DTOs; não expõem hashes de senha, convites, versões de sessão nem navegações de entidades do banco.

Erros: 400 para formato inválido/CSRF; 401 sem sessão válida; 403 sem permissão; 404 para recurso inexistente ou aviso que não pertence à conta; 409 para regra ou revisão conflitante; 429 para excesso de tentativas. Não repita automaticamente uma operação com uma nova chave após timeout: consulte o estado ou repita a mesma chave.

## Consultas

| Método e caminho | Uso |
|---|---|
| `GET /api/salao/mesas` | Id, nome, capacidade, habilitada, situação, versão e atendimento/garçom atual. Portaria, garçom e gestão. |
| `GET /api/salao/grupos?dia=2026-11-07` | Reservas e fila aguardando chegada/acomodação. O dia é opcional. |
| `GET /api/salao/garcons` | Responsáveis disponíveis para acomodação/transferência. |
| `GET /api/salao/cardapio` | Produtos e preços atuais para o salão. Garçom e gestão. |
| `GET /api/salao/comandas/{id}` | Consumo, subtotal, desconto, total, versão, recebimentos e histórico. Garçom responsável ou gestão. |
| `GET /api/salao/cozinha` | Itens ativos de salão e delivery, etapas e versões. Cozinha e gestão. |
| `GET /api/salao/relatorio?dia=2026-11-07&comparar=2026-11-06` | Comparação de vendas físicas. Gerente e administrador. |
| `GET /api/equipe/notificacoes?pagina=1` | Lista paginada da própria conta. |
| `GET /api/equipe/notificacoes/{id}` | Mensagem completa da própria conta. Inclui, quando aplicável, `atendimentoId`, `pedidoId` e `reservaCodigo`. |

## Portaria e mesas

| Caminho POST | Campos principais |
|---|---|
| `/api/salao/fila` | `chaveSolicitacao`, `nome`, `pessoas`, `telefone` opcional. Registra um grupo já presente. |
| `/api/salao/chegada` | `chaveSolicitacao`, `codigo` da reserva, `versao`. Registra chegada, sem ocupar a mesa. |
| `/api/salao/fila/chamar` | `chaveSolicitacao`, `mesaId`. Chama o primeiro grupo compatível com a mesa. |
| `/api/salao/acomodar` | `chaveSolicitacao`, `mesaId`, `versao` da mesa, `reservaCodigo` opcional, `nomeGrupo`/`pessoas` para atendimento direto, `garcomId` opcional para a gestão. Retorna `{ id }` da comanda. |
| `/api/salao/reservas/cancelar` | `chaveSolicitacao`, `codigo`, `versao`, `motivo`. Somente gestão. |
| `/api/salao/mesas/configurar` | `chaveSolicitacao`, `mesaId` (zero cria), `versao`, `nome`, `capacidade`, `ativa`. Somente gestão. |

O grupo escolhido na acomodação precisa ter a chegada registrada. Se estiver chamado para uma mesa, a acomodação deve usar aquela mesa. `habilitada`/`ativa` significa que a mesa faz parte da operação; **Livre**, **Ocupada**, **Aguardando garçom**, **Aguardando limpeza** e **Indisponível** descrevem sua disponibilidade atual.

## Rodadas, correções e entrega

Exemplo de nova rodada, depois de consultar a versão da comanda:

```json
{
  "chaveSolicitacao": "UUID-NOVO-DA-ACAO",
  "atendimentoId": "UUID-DA-COMANDA",
  "versao": "UUID-DA-VERSAO-EXIBIDA",
  "itens": [
    { "produtoId": 10, "quantidade": 2, "observacao": "Sem cebola" },
    { "produtoId": 11, "quantidade": 1, "observacao": null }
  ]
}
```

| Caminho POST | Campos além de `chaveSolicitacao`, `atendimentoId` e `versao` |
|---|---|
| `/api/salao/rodadas` | `itens`: de 1 a 30 linhas; produto, quantidade de 1 a 30 e observação até 500 caracteres. O servidor calcula o preço. |
| `/api/salao/itens/corrigir` | `itemId`, `produtoId` opcional, `quantidade` de 0 a 30, `observacao`, `motivo`. Zero cancela; positivo cria substituição. Depois de iniciar preparo, somente gestão. |
| `/api/salao/itens/entregar` | `itemId` pronto para retirada. |
| `/api/salao/comandas/transferir` | `garcomId` do novo responsável. Somente gestão. |
| `/api/salao/comandas/desconto` | `tipo` (0 reais, 1 percentual), `valor` e `motivo`. Zero remove o desconto. Somente gestão. |
| `/api/salao/comandas/fechar` | `pagamentoExternoConferido: true`, `pagamentos`: lista de `meio`, `valor`, `referencia` opcional até 100 caracteres. A soma deve ser igual à conta. |
| `/api/salao/comandas/liberar` | Sem campos adicionais. A conta precisa estar fechada e aguardando limpeza. |

Meios de pagamento: 0 dinheiro, 1 cartão de crédito, 2 cartão de débito, 3 Pix, 4 outro. São registros de recebimento externo. Não envie número de cartão, código de segurança, senha ou dados da maquininha. Em cortesia integral/conta zerada, a lista pode estar vazia; a conferência ainda é obrigatória.

## Cozinha

`POST /api/salao/cozinha/preparo`:

```json
{
  "chaveSolicitacao": "UUID-NOVO-DA-ACAO",
  "origem": "salao",
  "itemSalaoId": "UUID-DO-ITEM",
  "itemDeliveryId": null,
  "versao": "UUID-DA-VERSAO-DO-ITEM",
  "acao": "iniciar"
}
```

Para delivery, use `origem: "delivery"`, `itemDeliveryId` numérico e `itemSalaoId: null`. A versão é a do item retornado pela fila. A ação pode ser `iniciar` ou `pronto`, uma etapa por vez. Marcar pronto no salão cria o aviso de retirada; a entrega à mesa é registrada pelo garçom em outro endpoint. No delivery, pronto não altera o pedido para Entregue.

Estados de preparo retornados como número: 0 Na fila, 1 Em preparo, 2 Pronto para retirada, 3 Entregue à mesa, 4 Cancelado. Estados da comanda: 0 Aberto, 1 Aguardando limpeza, 2 Encerrado, 3 Cancelado. Reservas mantêm os valores existentes e acrescentam 7 Aguardando chegada. O aplicativo deve usar esses estados para desenhar a interface, mas o servidor sempre valida a permissão e a transição.

## Avisos no aplicativo

Consulte a lista com a sessão do funcionário. A resposta traz somente os avisos aos quais aquela conta tem acesso. Para registrar leitura:

`POST /api/equipe/notificacoes/ler` com `{ "id": "UUID-DO-AVISO" }` e CSRF.

Um item pronto identifica a mesa, quantidade, nome e observações, com `atendimentoId` para abrir a comanda. Transferir o atendimento gera aviso ao novo responsável. O aplicativo pode consultar periodicamente enquanto estiver em uso; esta versão não entrega push com o aplicativo fechado.

## Gestão de funcionários

| Método e caminho | Uso |
|---|---|
| `GET /api/equipe/funcionarios` | Listar funcionários e situação do convite. |
| `GET /api/equipe/funcionarios/{id}` | Dados de edição e `versao` para conferir edição concorrente, independente da versão interna da sessão. |
| `POST /api/equipe/funcionarios` | Criar/editar: `id` opcional, `versao` na edição, `nome`, `email`, `telefone` opcional, `cargo` e `ativo`. |
| `POST /api/equipe/funcionarios/reenviar-convite` | `funcionarioId`, `versao`. Invalida o convite anterior e envia outro. |

Cargos numéricos no cadastro: 2 gerente, 3 garçom, 4 portaria, 5 cozinha. O gerente só administra os cargos operacionais; o administrador pode criar/editar gerentes. O cargo 1 Administrador não é concedido por esse cadastro. A resposta do salvamento contém `id` e `conviteEnviado`; não contém senha nem link de convite. O funcionário define a senha pelo link recebido no e-mail.

## Concorrência e histórico

A mesa tem uma chave única para impedir duas comandas simultâneas. A trava de sala é compartilhada no banco, e versões impedem que uma aba antiga sobrescreva consumo, preparo ou fechamento. Uma mesma solicitação reenviada retorna o resultado anterior sem duplicar itens ou recebimentos. Correções, descontos, mudanças de responsável e fechamento ficam registrados com data e responsável.

Nenhuma rota da API deve ser liberada por confiar apenas em botões escondidos. Os mesmos serviços usados pelo site verificam cargo, situação atual da conta, dono da comanda, etapa e valores. O aplicativo Android precisa tratar respostas de conflito e recarregar os dados.
