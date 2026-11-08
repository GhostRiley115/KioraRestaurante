# Administração e relatórios gerenciais

## O que mudou

A administração passa a ter um menu organizado por tarefa: acompanhamento, operação física e gerenciamento. Cupons distinguem usos reservados de reservas de mesa. No computador o menu fica na lateral; no celular começa recolhido. As cores acompanham os temas existentes. As telas de compra do cliente permanecem com seu layout atual.

A visão geral mostra a operação atual (fila de preparo, preparo, entrega, mesas ocupadas/livres/em limpeza e grupos na fila), avisos de pendências financeiras e os resultados do mês até hoje. Os atalhos levam à tela correspondente. A atualização acontece ao abrir/atualizar a página; o painel não promete atualização em tempo real.

O relatório permite escolher dois períodos independentes, limitar o canal, consultar atalhos de hoje/últimos sete dias/mês atual/mês anterior completo e exportar as vendas em CSV. Cada período admite até 366 dias. A lista de vendas tem 20 registros por página; a exportação abrange todas as vendas do período selecionado, sem depender da página. O ranking mostra os dez produtos mais vendidos.

Os gráficos apresentam vendas diárias por canal e movimento por hora. Os valores respondem a toque, foco e mouse; o gráfico diário também oferece tabela de dados. Gráficos e tabelas maiores têm rolagem própria, sem alargar a página inteira. A comparação apresenta totais, quantidade de vendas e ticket médio.

Pedidos de delivery agora têm listas separadas de **Em andamento**, **Concluídos e cancelados** e **Todos**. Há atalhos para recebidos, em preparo e em entrega. Em andamento vem primeiro recebido, depois preparo e entrega; dentro de cada etapa, o mais antigo aparece primeiro. Histórico e consulta completa mantêm os mais recentes primeiro. A busca pode ser expandida e preserva a lista na paginação. O acesso ao histórico de um cliente mostra todas as situações.

Produtos têm ações destacadas para editar e aplicar/editar promoção. As ações de disponibilidade e ativação ficam em um grupo identificado, com as confirmações já existentes. A tela de desconto simula o preço final antes de salvar; cálculo e validação definitivos continuam no servidor. Após cadastrar um produto, o administrador volta ao gerenciamento com mensagem de sucesso.

## Critérios das vendas

- Horário de Brasília, convertendo os limites dos dias para UTC; o fim do período é inclusivo para o usuário.
- Delivery: data de criação do pedido, com pagamento aprovado, ou dinheiro já entregue com pagamento pendente. Pedidos cancelados, pagamentos recusados/cancelados/reembolsados/em conferência e alterações financeiras em andamento não entram. Um ajuste pendente pode retirar temporariamente uma venda do relatório até ser concluído.
- Salão: data e total do fechamento da conta. Mesas abertas não são vendas confirmadas. Contas aguardando limpeza e encerradas entram uma única vez; itens cancelados são excluídos.
- O total delivery inclui frete. O relatório mostra frete separado e o total sem frete. No salão os recebimentos são registros externos, sem processamento de cartão pelo sistema.
- Ticket médio: total dividido pela quantidade de pedidos de delivery e contas físicas elegíveis. Cortesias fechadas são contas com valor zero e preservam o consumo.
- Comparação padrão: mesmas datas do mês anterior, ajustadas ao último dia do mês quando necessário. Os intervalos aparecem explicitamente. Não há divisão por zero: sem vendas no período de comparação, a variação percentual fica indisponível.

Este relatório é gerencial. Não informa lucro, taxas do Mercado Pago, liquidação bancária ou fluxo de caixa por data efetiva de aprovação, pois esses dados não são registrados com esses critérios no modelo atual.

## Produtos, promoções e reservas

Os preços e nomes vêm do histórico das vendas, sem recalcular com o catálogo atual. O ranking agrupa pelo produto e usa o nome histórico mais recente no período. Combos contam como produtos vendidos, sem somar novamente seus componentes.

Unidades em promoção são aquelas com preço original registrado maior que o preço unitário vendido. O desconto de catálogo é a diferença multiplicada pela quantidade. Cupons do delivery e descontos concedidos na comanda aparecem separados, evitando atribuir um desconto de conta a cada produto.

A migration `HistoricoDescontosSalao` acrescenta somente dois campos anuláveis em `ItensComanda`: preço original unitário e percentual. Novos lançamentos os preenchem; correções de quantidade do mesmo produto preservam o histórico original. Vendas antigas sem esses dados continuam na receita, mas recebem aviso de cobertura incompleta e não são consideradas unidades promocionais por suposição. Não há preenchimento retroativo a partir do preço atual.

Horários de venda usam criação do pedido elegível no delivery e fechamento no salão. Horário mais reservado considera o horário **agendado de chegada**, exclui reservas canceladas/expiradas e não conta fila sem agendamento. Reservas pertencem ao salão e permanecem visíveis independentemente do canal financeiro escolhido. Empates exibem a primeira hora e o gráfico permite consultar todas. Não se trata da hora em que a pessoa acessou o site para reservar.

Mesas chamadas pela portaria ainda aguardando acomodação não aparecem como livres. Os contadores operacionais são da situação atual, independentemente do período financeiro filtrado.

## Segurança e ativação

Relatórios e exportação exigem Administrador tanto no controlador quanto no serviço, conferindo situação e cargo no banco. Gerente e demais funcionários continuam com suas permissões anteriores; não recebem acesso ao relatório completo. Consultas não expõem senhas, tokens ou dados de cartão. CSV neutraliza possíveis fórmulas em textos cadastrados, conserva acentos e usa moeda/data brasileiras.

Comentários anteriores foram preservados. A atualização do banco é aditiva, preservando os pedidos. Deve ser aplicada antes de executar/publicar a versão nova, porque as consultas de comandas passam a incluir os campos históricos. Não há novas chaves nem configurações externas.

## Verificação

Foram aprovados 271 testes .NET e 18 testes JavaScript. As páginas de gerenciamento e a exportação foram conferidas por HTTP; relatórios e CSV foram bloqueados para cliente, gerente, garçom, portaria, cozinha e acesso anônimo.

Os testes cobrem a soma por canal, separação do frete e descontos, cancelamentos/estornos/pendências, pagamento em dinheiro, horário de Brasília, dias sem movimento, filtros, comparação sem base, autorização, histórico de preços, reservas, correções presenciais e ordenação/separação dos pedidos. A verificação visual usa somente um servidor e banco fictícios isolados, sem criar vendas, comunicados ou funcionários reais no Aiven.

Para volumes muito maiores, convém substituir a agregação em memória dos registros do intervalo por consultas de agregação no banco e exportação em fluxo. Hoje há limite de duração do período e projeção apenas dos campos necessários, adequados ao escopo acadêmico atual.
