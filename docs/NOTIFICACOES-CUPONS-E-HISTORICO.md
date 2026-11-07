# Notificações, cupons e ajustes no histórico — 07/11/2026

## O que foi implementado

- A troca de endereço não exibe mais o texto sobre os 30 minutos de preparo. O cálculo de rota e de cobrança continua funcionando.
- No checkout, o campo de cada produto se chama **Observações (opcional)**. O leitor de tela identifica também o produto a que ele pertence.
- Os detalhes do pedido, do cliente e do administrador, exibem as fotos dos produtos. Novas compras guardam a referência da foto usada na compra. A limpeza do Cloudinary considera essas referências e preserva imagens do histórico.
- O link **Continuar no cardápio** foi retirado dos detalhes do pedido.
- Os comentários que já estavam no código foram preservados.

## Como usar as notificações

Após a ativação, contas autenticadas terão um sino na navegação e a opção **Notificações** no menu da conta. A lista guarda os avisos, mostra quais não foram lidos e permite abrir os detalhes ou marcar a leitura.

O cliente recebe avisos quando o pedido é registrado, começa o preparo, sai para entrega, é entregue ou cancelado, e quando o pagamento online é aprovado. Alterações da própria reserva também geram aviso. Administradores recebem avisos de novos pedidos e reservas, incluindo reservas feitas por visitantes.

Em **Administração → Pedidos → Detalhes**, o administrador pode avançar uma etapa por vez: Recebido → Em preparo → Saiu para entrega → Entregue. Pagamento online precisa estar aprovado; uma troca de endereço ainda pendente bloqueia o avanço. Confirmar a entrega de pedido em dinheiro também confirma que o valor foi recebido. A regra de cancelamento do cliente continua restrita a Recebido; o estorno de um cancelamento pago continua sujeito à conferência do restaurante.

Os eventos são gravados na mesma transação da mudança do pedido/reserva. Um serviço de fundo distribui os avisos a cada 15 segundos e o sino consulta a contagem a cada 30 segundos enquanto a aba está visível. Os avisos podem levar alguns segundos para aparecer e ficam guardados mesmo se o site reiniciar. Retentativas não criam cópias da mesma notificação. Uma falha em um aviso não bloqueia os demais.

Estes avisos são internos ao site. Não foram acrescentados e-mail, SMS ou notificações do navegador.

## Como publicar um comunicado

1. Abra **Administração → Comunicados → Preparar notificação**.
2. Escreva o título e a mensagem.
3. Escolha **Todos os clientes ativos**, ou **Todas as contas ativas, incluindo administradores**.
4. Se a mensagem for sobre um cupom, selecione o cupom associado.
5. Clique em **Conferir prévia** e revise o texto e o público.
6. Clique em **Publicar notificação** para efetivamente distribuir o comunicado.

A prévia vale por 10 minutos. Alterações no texto, público ou regras do cupom exigem nova conferência. O histórico administrativo mostra os destinatários e quantas contas marcaram o aviso como lido. Contas criadas depois da publicação não recebem retroativamente o comunicado.

Nenhum comunicado foi enviado às contas do site publicado durante o desenvolvimento. A publicação foi testada somente com contas fictícias em banco local isolado.

## Como criar e divulgar cupons

1. Abra **Administração → Cupons → Criar cupom**.
2. Defina o código, a descrição e, se desejar, o nome do parceiro. Use um código diferente para cada parceiro quando quiser medir o desempenho individualmente.
3. Escolha porcentagem ou valor em reais e informe o desconto. Valores aceitam vírgula ou ponto decimal, por exemplo **5,50** ou **5.50**.
4. Defina o mínimo em produtos e, opcionalmente, um teto de desconto.
5. Defina início e fim da validade, no horário de Brasília.
6. Defina o limite total, se houver, e o limite por cliente.
7. Salve. **Editar** permite ajustar as regras ou pausar o cupom. O código de um cupom existente é mantido; para outro código, crie outro cupom.
8. Use **Divulgar** ou **Preparar divulgação** para abrir um comunicado com o cupom associado. Revise e publique a prévia.

No checkout, o cliente informa o código e clica em **Aplicar**. Pode remover o cupom antes de confirmar. É permitido um cupom por pedido, sobre o preço atual dos produtos, incluindo descontos do catálogo já aplicados. O frete não recebe desconto. O desconto nunca supera o subtotal dos produtos.

O servidor confere validade, regras, cliente, produtos e limites novamente na confirmação. A cotação do cupom dura 15 minutos. Uma alteração do carrinho ou das regras exige aplicar novamente.

### Reservas, utilizações e cancelamentos

- Confirmar o pedido reserva uma utilização. Um pedido ainda não pago continua ocupando essa cota até ser cancelado.
- O pagamento online aprovado confirma o uso. Em dinheiro, o uso é confirmado na entrega.
- Um cancelamento libera a cota, inclusive se o uso já havia sido confirmado, e permanece registrado no relatório.
- Reservas e confirmações contam para os limites total e por cliente. A mesma compra reenviada não consome outra cota.
- A última cota é protegida por uma transação e por bloqueio da linha do cupom, evitando que duas instâncias do site a reservem ao mesmo tempo.
- Alterar ou pausar um cupom não muda o desconto de pedidos já criados.

Em **Cupons → Relatório**, o administrador vê reservas, confirmações, cancelamentos, descontos dos usos confirmados e o histórico por pedido. A identificação do parceiro é a registrada na compra. A lista mostra os 100 cupons mais recentes; o histórico de cada cupom tem paginação de 50 usos.

## Pagamento e alteração de endereço

O desconto e o código são guardados no pedido. A cobrança online usa o subtotal com desconto e acrescenta o frete integral. Quando há cupom, os produtos são agrupados em uma linha no Mercado Pago para evitar diferenças de arredondamento entre preço unitário e quantidade; os detalhes completos continuam no pedido da Kiora. Um cupom que cobre todos os produtos deixa apenas a taxa de entrega na cobrança.

A troca de endereço mantém o desconto original e ajusta apenas a diferença da entrega. Os testes cobrem cobrança e devolução dessa diferença com cupom aplicado. Não foram realizadas cobranças nem devoluções reais nesta tarefa.

Referência da conferência de itens e total: [Orders API — múltiplos itens](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-orders/additional-settings/create-order-for-multiple-items?scope=prod).

## Dados e implantação

A migration **20261107190616_NotificacoesCuponsEFotosHistoricas** acrescenta as tabelas de cupons, usos, eventos, notificações e leituras, além dos campos de cupom no pedido e de foto no item.

Os pedidos existentes começam com desconto de cupom zero. Para fotos de compras anteriores, a migration registra a imagem disponível atualmente no catálogo; ela não consegue recuperar uma foto que já havia sido substituída antes desta versão. Novas compras preservam a referência usada no momento do pedido.

Não há remoção de tabelas, colunas ou registros no caminho de atualização. Não são criados cupons promocionais nem comunicados no banco compartilhado pela migration. Também não são disparados avisos retroativos de todos os pedidos antigos.

A ativação exige aplicar essa migration no Aiven antes de executar a nova versão e publicá-la no Render. Nenhuma chave nova de serviço externo é necessária.

## Verificação realizada

- Compilação sem avisos nem erros.
- 212 testes .NET aprovados, incluindo limite concorrente da última cota, validação do cliente/carrinho, retentativas, falha atômica na compra e na distribuição, permissão de leitura, proteção da prévia, descontos e diferença do frete, fotos do histórico e separador decimal.
- 18 testes JavaScript aprovados.
- Modelo atual e script da migration conferidos com o provedor MySQL, sem conexão com o banco compartilhado.
- Navegador com dados fictícios: cupom aplicado no checkout, observações, foto nos detalhes, troca de endereço sem o texto removido, avanço do pedido, notificações e leitura, comunicado com cupom, cadastro em reais com vírgula e relatório de uso confirmado na entrega.
- Telas conferidas em desktop e celular de 390 px, nos temas claro e escuro.
