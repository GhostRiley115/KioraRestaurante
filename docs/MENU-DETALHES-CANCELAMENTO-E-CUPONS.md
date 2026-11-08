# Administração: menu, detalhes do delivery, cancelamento e ajuda dos cupons

## Conta da equipe

O painel da conta separa atalhos de gestão, operação e conta pessoal conforme o cargo. O administrador acessa Pedidos (de todos os clientes), visão geral, relatórios, salão, cozinha, recepção, produtos, cupons, comunicados, clientes e equipe. Os links pessoais de pedidos, reservas e endereços ficam para clientes. A navegação lateral administrativa continua disponível nas telas de gerenciamento.

## Detalhes do pedido

A tela mostra andamento, próxima ação, itens com fotos e observações, composição histórica dos combos, preparo de cada item, cobrança, cliente, endereço e histórico das solicitações. Preços promocionais e cupons usam os registros da compra. O atalho para pedidos do cliente inclui todas as situações.

O avanço explica os bloqueios de pagamento, solicitação em andamento e itens ainda não prontos. Na entrega em dinheiro, a confirmação informa expressamente que também registra o recebimento. A decisão final continua validada no servidor.

## Cancelamento administrativo

Regra aprovada: administrador ativo pode cancelar em Recebido ou Em preparo; Saiu para entrega e Entregue bloqueiam o cancelamento. Motivo obrigatório, com até 300 caracteres, registrado junto de data e identificação do responsável. A conta do cliente pode estar inativa; isso não impede a administração de resolver um pedido existente. Gerentes e outros cargos não recebem esta permissão administrativa. O cliente continua podendo cancelar somente em Recebido.

A operação usa a mesma proteção contra concorrência e reprocessamento dos ajustes do pedido. Uma solicitação pendente bloqueia outras alterações. Cobranças online pendentes são conferidas e encerradas pelo fluxo existente antes de confirmar o cancelamento. Se a conferência não concluir, a tela não anuncia pedido cancelado. Pedidos pagos permanecem com o registro real do pagamento e uma indicação de devolução a conferir; não há estorno automático nesta ação. Pagamentos antigos sem order são preservados, inclusive conferência ou reembolso já registrados.

O cliente recebe a notificação automática existente. A cozinha, os gerentes e os administradores ativos recebem um aviso para interromper o preparo, com o número do delivery e o motivo. O aviso e a mudança do pedido são gravados juntos e não se duplicam em retentativas. Esse aviso operacional não entra na lista de comunicados promocionais.

Migration: `20261108043941_CancelamentoAdministrativo`. Acrescenta somente dois campos opcionais em OperacoesPedido: AdministradorSolicitanteId e AdministradorSolicitanteNome. Não remove nem altera dados dos pedidos existentes. Operações antigas permanecem sem identificação administrativa; o histórico só atribui cancelamento ao cliente quando há operação correspondente.

## Ajuda para preencher cupons

O formulário se divide em identificação, benefício e regras. Cada campo tem explicação vinculada para leitores de tela, exemplos e indicação de opcionalidade. A orientação do valor acompanha a seleção de porcentagem ou reais. Há um exemplo completo de campanha e explicação de reservas, utilizações confirmadas e cancelamentos.

A ajuda esclarece que o desconto incide nos produtos e não no frete; mínimo 0 dispensa compra mínima; teto e limite total vazios dispensam esses limites adicionais. O uso individual pode ser único ou reutilizável até expirar, sempre respeitando validade, situação e limite global. O nome PRIMEIRACOMPRA não cria automaticamente uma regra para clientes novos. Nenhuma regra financeira do cupom foi alterada.

## Validação

- 324 testes .NET aprovados, incluindo autorização, etapas, motivo, concorrência, falha de gravação e retentativa, pagamento online simulado e histórico.
- 18 testes JavaScript aprovados.
- Build sem erros ou avisos; modelo compatível com a migration MySQL, com atualização somente aditiva.
- Revisão visual em ambiente local isolado, com banco temporário e dados fictícios. Nenhum pedido real cancelado e nenhum estorno ou comunicado promocional realizado durante os testes.
- Comentários anteriores preservados.
