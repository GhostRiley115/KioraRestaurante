# Complementos do checkout e dos pedidos

Implementação de novembro de 2026. Preserva os comentários existentes e complementa o fluxo anterior de endereços e entrega.

## Observações e fotos

O checkout apresenta a foto de cada produto e um campo opcional de até **500 caracteres** para preferências de preparo. Exemplo: “Sem cebola e pouco molho”. A observação vale para todas as unidades daquela linha; se houver preferências diferentes, o cliente deve descrevê-las no texto.

O servidor confere se a observação pertence a um produto efetivamente presente no carrinho e grava o texto em `ItemPedido.Observacao`. Os detalhes do cliente e do administrador exibem o registro histórico. O texto é escapado pelo Razor para impedir execução de HTML. Não é enviado como instrução financeira ao Mercado Pago.

As fotos do checkout reutilizam as URLs já cadastradas, inclusive as imagens do Cloudinary. Uma imagem ausente ou que não carregar usa uma ilustração local neutra.

## Previsão estimada

O prazo é **30 minutos de preparo por pedido + tempo estimado do percurso de carro**, retornado pelo openrouteservice/HeiGIT. Não soma 30 minutos para cada unidade.

O tempo do percurso é arredondado para cima em minutos. Exemplo: 10 minutos e 12 segundos viram 11 minutos; prazo exibido de aproximadamente 41 minutos.

O checkout mostra a composição do prazo após calcular a entrega. O pedido guarda a duração consultada e o preparo; seus detalhes exibem o horário estimado a partir da compra. Uma correção de endereço recalcula o percurso, mantendo o horário original da compra como referência para o preparo. Não há acompanhamento de trânsito ou do entregador em tempo real.

Pedidos antigos ficam sem estimativa histórica quando esse dado não foi registrado; não se inventa uma duração antiga. Pedidos entregues ou cancelados não mostram uma previsão futura.

## Área e taxas aprovadas

A origem continua sendo Rua dos Pinheiros, 1234, Pinheiros, São Paulo/SP. A área foi ampliada para **35 km pelas ruas**.

| Distância | Taxa |
|---|---:|
| Até 5 km | R$ 5,00 |
| Acima de 5 até 10 km | R$ 8,00 |
| Acima de 10 até 15 km | R$ 12,00 |
| Acima de 15 até 20 km | R$ 16,00 |
| Acima de 20 até 25 km | R$ 20,00 |
| Acima de 25 até 30 km | R$ 24,00 |
| Acima de 30 até 35 km | R$ 28,00 |
| Acima de 35 km | Não aceita |

São valores fictícios aprovados para o TCC. O limite e as taxas são conferidos no servidor, tanto na compra quanto na correção de endereço.

## Cancelamento pelo cliente

O cancelamento pelo site é permitido somente em **Recebido**, antes de começar o preparo. A tela explica a regra e exige o clique em **Confirmar cancelamento**, com motivo opcional. O servidor reconfere a etapa e a versão, inclusive se outra requisição alterar o pedido enquanto o cliente estiver na página.

Quando existir uma cobrança online ainda não paga, o sistema consulta e solicita seu encerramento no Mercado Pago. Se não conseguir confirmar o resultado, deixa a solicitação em conferência. Não informa que uma cobrança incerta foi cancelada.

Quando o pagamento estiver aprovado, o pedido pode ser cancelado dentro da regra, mas **o estorno do cancelamento exige conferência do restaurante**, conforme a opção aprovada. O pagamento permanece identificado como aprovado até haver informação real do provedor; o sistema não promete uma devolução automática. Cliente e administrador veem o aviso. Se houver cobranças adicionais de entrega, elas também precisam ser consideradas na conferência.

## Correção do endereço e da cobrança

Nos detalhes, **Corrigir endereço de entrega** fica disponível em Recebido e Em preparo. Em Saiu para entrega, a tela informa que a alteração pelo site está bloqueada. Pedidos encerrados também não permitem a alteração.

O cliente calcula a nova rota e confere a nova taxa e o total antes de confirmar. Uma solicitação em andamento não substitui o endereço válido até ser concluída.

- **Dinheiro, ainda não pago:** atualiza o endereço e o total a pagar na entrega.
- **Pagamento online ainda não aprovado:** confirma o encerramento da cobrança anterior, preserva uma cópia no histórico da solicitação e permite gerar um novo checkout com o total correto.
- **Pagamento aprovado e taxa igual:** mantém o pagamento e atualiza o endereço.
- **Pagamento aprovado e taxa maior:** cria uma cobrança adicional somente pela diferença. O cliente a abre pelo botão Pagar diferença com Mercado Pago. O endereço muda após confirmação do valor pelo provedor, se a etapa do pedido ainda permitir.
- **Pagamento aprovado e taxa menor:** solicita a devolução somente da diferença, usando as transações efetivamente pagas. O endereço muda após confirmação do processamento. O prazo de crédito depende do meio de pagamento.

As operações têm corpos e chaves de idempotência persistidos. Uma nova tentativa após timeout reutiliza a mesma operação. As devoluções identificam os reembolsos anteriores e não repetem uma devolução apenas porque o valor é igual.

O servidor confere vendedor, aplicação, ambiente, referência, moeda e valor das cobranças. Também confere os valores e identificadores de devolução antes de atualizar o pedido. As cobranças e devoluções adicionais ficam registradas separadamente do pagamento original.

Uma cobrança adicional não paga pode ser encerrada por **Desistir da alteração**, mantendo o endereço anterior. Solicitações sem conclusão expiram após 30 minutos; uma movimentação financeira já confirmada exige conferência, em vez de ser descartada silenciosamente.

Se o pedido sair para entrega durante o ajuste, o servidor impede que o endereço seja sobrescrito. Se algum valor já tiver sido movimentado, a solicitação fica em conferência e oferece ajuda para o restaurante resolver a diferença. Falhas, permissões insuficientes, saldo indisponível ou respostas divergentes do provedor nunca são apresentados como ajuste concluído.

Um serviço de fundo retoma solicitações pendentes; o cliente também pode usar Atualizar andamento. Webhooks assinados ajudam a atualizar as cobranças adicionais. Não se inicia uma nova solicitação a partir de uma visita GET à página.

## Preciso de ajuda

O botão aparece no checkout, no pagamento, nos detalhes, no histórico e no andamento de solicitações. Abre o WhatsApp em **+55 11 94276-4888**, com uma mensagem inicial e o número do pedido quando disponível. O cliente decide enviar a mensagem; o sistema não envia mensagens automaticamente.

## Banco e implantação

A migration **20261106233758_ComplementarCheckoutEPedidos** acrescenta os campos de duração, preparo, versão, revisão financeira e cancelamento; cria as tabelas de solicitações e movimentos financeiros. Não apaga pedidos, itens ou endereços existentes. A observação já tinha uma coluna e passa a ser preenchida pelo checkout.

Não são necessárias novas chaves. Permanecem as configurações locais e do Render para Geoapify, HeiGIT e Mercado Pago.

A migration deve ser aplicada antes de iniciar a versão nova. Ao publicar, confirmar que não há outras migrations inesperadas pendentes. Não executar `Down` no banco compartilhado como tentativa de corrigir uma publicação, pois isso apagaria os novos registros.

## Validação e limites dos testes

A suíte completa passou em **161 testes**.

Na consulta real com o endereço público do MASP, a rota retornou 4.474,7 metros e cerca de 9 minutos de percurso (arredondados para cima), resultando em aproximadamente 39 minutos com o preparo. Esse resultado não considera trânsito em tempo real.

Os testes usam SQLite e respostas simuladas para cancelamento, troca de faixa, cobrança adicional, devolução parcial, devoluções iguais, repetição, timeout após processamento, falha ao salvar, propriedade do pedido e mudança de etapa. A interface usa um host temporário com dados fictícios para confirmar foto, observação, estimativa, alteração de endereço, cancelamento e links de ajuda.

As consultas reais de rota usam apenas endereços públicos. As operações financeiras novas são verificadas por simulação; não representam uma cobrança ou devolução real já realizada. A confirmação de um pagamento de teste no checkout do provedor deve ser feita pelo usuário. Se o ambiente de teste do Mercado Pago não permitir uma operação, o fluxo deverá permanecer em conferência.

Referências oficiais: [cancelar order](https://www.mercadopago.com.br/developers/pt/reference/online-payments/checkout-pro/cancel-order/post), [reembolso total/parcial](https://www.mercadopago.com.br/developers/pt/reference/online-payments/checkout-pro/refund-order/post), [reembolsos e cancelamentos](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-orders/refunds-cancellations?scope=prod).
