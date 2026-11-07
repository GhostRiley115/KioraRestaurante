# Salão, portaria, cozinha e equipe

## Fluxo combinado

1. O cliente envia uma solicitação de reserva, que fica **Aguardando chegada**. Ela não garante uma mesa específica nem um horário de liberação de outra mesa.
2. A portaria registra a chegada. O grupo entra na fila presencial, por horário de chegada. A chamada respeita a capacidade da mesa: um grupo menor pode ser chamado antes de um grupo que não cabe na mesa disponível.
3. A portaria pode chamar o primeiro grupo compatível para uma mesa livre. A chamada não abre a comanda nem confirma que o grupo já foi acomodado.
4. O garçom acompanha o grupo até a mesa e confirma a acomodação. A mesa passa a estar **Ocupada** e uma comanda é aberta. O garçom também pode iniciar um atendimento direto, sem reserva, para um grupo já presente.
5. O garçom escolhe os produtos e as observações no cardápio da comanda. Cada envio é uma nova rodada; a conta continua aberta para novos pedidos de bebida, sobremesa e outros produtos.
6. Na cozinha, o funcionário inicia o preparo e marca o item como **Pronto para retirada**. O garçom responsável recebe uma notificação com a mesa, a quantidade, o produto e as observações.
7. O garçom busca o item e confirma **Entregue à mesa**. Isso não fecha a conta.
8. Quando o grupo pedir a conta, o garçom confere os itens e registra o pagamento recebido fora do sistema. Pode dividir o registro entre dinheiro, cartão, Pix ou outro meio. A soma precisa corresponder ao total.
9. A conta fechada fica no histórico e a mesa passa a **Aguardando limpeza**. Ela ainda não pode receber outro grupo.
10. Depois da limpeza, o garçom responsável, o gerente ou o administrador confirma **Liberar mesa**. A mesa volta a ficar livre.

Não há limite automático de permanência nem liberação de mesa ocupada pelo relógio. O horário de funcionamento ainda controla novas solicitações e acomodações. A tolerância para chegar à reserva continua em 15 minutos; a chamada de um grupo dura 10 minutos. Uma chamada vencida volta à fila enquanto o restaurante estiver aberto; o atendimento já acomodado nunca expira.

## Acesso no computador

Após entrar na conta de funcionário, o sistema abre **Operação do restaurante**, também disponível no menu da conta. O administrador pode abrir a mesma operação pelo painel administrativo.

| Cargo | Funções |
|---|---|
| Portaria | Ver disponibilidade de mesas, consultar grupos, registrar chegada, incluir grupo presencial e chamar o primeiro grupo compatível. |
| Garçom | Acomodar grupos, operar suas próprias comandas, lançar rodadas e observações, corrigir itens antes do preparo, confirmar entrega, fechar a conta e liberar a mesa limpa. |
| Cozinha | Ver a fila de salão e delivery, iniciar preparo e marcar pratos prontos. Receber avisos de correções. |
| Gerente | Toda a operação física: comandas de qualquer garçom, transferências de responsável, correções após início do preparo, descontos, cancelamento de reservas, configuração de mesas, relatórios e cadastro dos cargos operacionais. |
| Administrador | Todas as funções acima e a administração geral que já existia. Pode cadastrar também gerentes. |

O gerente não cria administradores nem gerencia o acesso de outro gerente; isso fica com o administrador. Contas de clientes não podem ser transformadas silenciosamente em contas de funcionário: cada funcionário deve usar um e-mail individual disponível.

## Correções, descontos e fechamento

O garçom só corrige itens ainda **Na fila**. Depois que o preparo começou, a correção exige gerente ou administrador, com motivo. O item anterior permanece no histórico como cancelado; uma substituição entra novamente na fila e a cozinha recebe um aviso. Corrigir o mesmo produto preserva seu preço registrado; substituir por outro produto usa o preço atual do novo produto.

Um desconto pode ser em reais ou porcentagem. O gerente/administrador informa um motivo; o histórico registra o responsável. Até 100% é permitido para cortesia. O desconto não ultrapassa o subtotal. Os cupons e pagamentos online do delivery ficam separados da comanda física.

O fechamento exige que todos os itens tenham sido entregues ou cancelados. A soma dos recebimentos externos deve ser igual ao total final. Uma cortesia integral pode fechar sem registrar um pagamento fictício. O sistema não se comunica com a maquininha, não cobra o cartão e não executa estornos de pagamentos presenciais.

Uma comanda fechada não pode ter consumo, desconto ou recebimentos alterados por estas telas. O histórico é preservado. A configuração de uma mesa ocupada, aguardando limpeza ou com grupo chamado exige resolver o atendimento/chamada primeiro.

## Funcionários e convite

Em **Operação do restaurante → Funcionários → Cadastrar funcionário**, informe nome, e-mail, cargo e telefone opcional. O funcionário recebe um convite da Brevo e define a própria senha. O link vale por 24 horas, é de uso único e só seu hash é salvo no banco.

Se o envio falhar, a tela informa que o cadastro foi preservado e oferece **Reenviar convite**. Um novo convite invalida o anterior. Alterações de cargo, acesso ou e-mail invalidam as sessões anteriores; mudança de e-mail exige novo convite. Para desativar um garçom, primeiro transfira seus atendimentos ou conclua a liberação das mesas sob sua responsabilidade.

O convite usa a chave e remetente Brevo existentes. O endereço público do link vem de `Aplicacao:UrlPublica` ou da configuração já existente `MercadoPago:UrlPublica`, sempre HTTPS. Não é necessário criar outra chave de pagamento ou de mapas.

## Cozinha e delivery

A cozinha tem duas colunas: **Restaurante físico** e **Delivery**. Exibe produto, quantidade, observações, composição do combo, mesa ou número do pedido, horário e etapa. A fila confere alterações a cada 15 segundos com a aba visível e atualiza a página quando não há ação em foco; a atualização manual continua disponível.

Pedidos online ainda não aprovados e pedidos com ajuste de endereço pendente não entram na fila. Pagamento em dinheiro pode entrar sem aprovação online. Um item de delivery pronto não significa pedido entregue: a expedição ainda precisa confirmar **Saiu para entrega** no painel administrativo. A saída é bloqueada até todos os itens estarem prontos.

## Notificações

O sino agora abre um painel na própria página com os títulos das dez notificações mais recentes. Não marca leitura apenas por abrir o painel. Ao escolher um título, a mensagem completa abre em outra página e é marcada como lida. Há um botão para consultar as informações relacionadas à mesa, reserva, pedido ou cupom.

O ícone fica dourado ao passar o mouse ou receber foco pelo teclado, respeitando a cor do tema atual. O histórico completo continua disponível pelo menu e pelo painel do sino.

Os avisos de prato pronto são destinados ao garçom responsável e à gerência/administração. Após transferência da mesa, o garçom anterior perde acesso aos avisos específicos daquela comanda; o novo responsável recebe um aviso para conferir o atendimento. Correções são avisadas à cozinha e à gerência. O aplicativo poderá consultar esses mesmos avisos pela API. Não foi implementado push com o aplicativo fechado.

## Relatórios

Em **Vendas presenciais**, escolha um dia e outra data para comparação. O relatório separa atendimentos que passaram pelo dia, mesas distintas utilizadas, novas acomodações, contas fechadas, subtotal dos produtos, descontos e vendas líquidas de desconto. As contas entram na data do fechamento pelo horário de Brasília, mesmo que o atendimento tenha começado antes.

Delivery não entra nesses valores. A lista permite consultar os itens, recebimentos e histórico de cada comanda fechada. A contagem de ocupadas agora mostra o estado atual, não uma reconstrução daquele dia passado.

## Implantação e compatibilidade

Migration: `20261107220445_OperacaoSalaoEquipeECozinha`.

A atualização acrescenta tabelas de atendimento, itens de comanda, recebimentos, auditoria e convites, além de campos de preparo no delivery, controle de versão nas mesas e vínculo de avisos ao atendimento. Não remove tabelas ou registros existentes.

Reservas antigas ainda confirmadas passam a **Aguardando chegada**, liberando a associação antecipada a uma mesa. Atendimentos que já estavam ocupados são mantidos em comandas abertas, inicialmente atribuídas a um administrador ativo para conferência/transferência. Nenhuma venda passada é inventada nessas comandas. Os itens antigos de delivery recebem etapas compatíveis com a situação do pedido, sem voltar indevidamente à fila.

O aplicativo Android não foi desenvolvido nesta tarefa. Os serviços e os endpoints foram preparados para ele, conforme o [contrato da API](API-EQUIPE-E-SALAO.md). O computador e o aplicativo usam as mesmas regras de permissão, revisão e gravação.

Todos os comentários existentes foram preservados. Foram mantidos trechos/comentários históricos do cálculo antigo de reserva para compatibilidade interna; esse cálculo não determina mais uma saída obrigatória da mesa.

## Validação desta versão

- Compilação sem avisos nem erros e 249 testes .NET aprovados.
- 18 testes JavaScript existentes aprovados e verificação de sintaxe dos scripts alterados.
- Fluxo completo de salão, novas rodadas, observações, preparo, retirada, entrega, desconto, pagamento dividido, fechamento, limpeza e liberação testados com dados fictícios.
- Concorrência de duas instâncias disputando a mesma mesa, reenvio de comando e formulário antigo conferidos.
- Convites testados com provedor de e-mail simulado: hash, expiração, uso único, reenvio, falha de envio e restrição de cargos. Não foram enviados convites a pessoas reais durante os testes.
- API local conferida por HTTP: login por formulário e cookie, CSRF obrigatório, revisão e retentativa, restrições de portaria/cozinha/cliente, consulta de funcionários e relatório pela gerência.
- Migration e modelo conferidos com o provedor MySQL sem acessar o banco compartilhado; atualização sem remoção de tabelas, colunas ou registros.
- Os dados de teste e as contas fictícias pertencem a um host temporário isolado e não fazem parte da aplicação publicada.
