# Interface do restaurante físico, cupons e períodos

## Navegação e acesso

Salão, comandas, cozinha, recepção, configuração de mesas e funcionários passam a usar a mesma estrutura de gerenciamento da administração, incluindo o menu lateral. No celular o menu começa recolhido. Em notebooks, o menu tem rolagem própria para não esconder as últimas funções.

O conteúdo do menu considera o cargo. Administrador mantém acesso completo; gerente tem as funções físicas; garçom vê salão e recepção; recepção vê sua fila; cozinha vê a produção. As verificações de acesso no servidor permanecem ativas: esconder um link não substitui autorização. Convites públicos para definir senha mantêm seu layout público e não mostram o gerenciamento.

## Salão e atendimento

- Cartões distinguem disponível, ocupada, limpeza e grupo chamado, com texto e cor.
- Busca por mesa/garçom e filtros por situação; garçom também pode mostrar suas mesas.
- O grupo chamado aparece na mesa destinada a ele. A acomodação abre a comanda; chegada na recepção e chamada não ativam a mesa.
- Configurações de mesas ocupadas, em limpeza ou com grupo chamado ficam bloqueadas na interface e no serviço.

A comanda separa **Consumo da mesa** e **Adicionar itens**. Os produtos do cardápio têm foto, preço e um formulário que se abre somente quando necessário. Busca ignora acentos e esconde categorias sem resultados.

Itens prontos recebem destaque e ação de confirmar entrega. Os já entregues podem ser recolhidos; itens cancelados permanecem no histórico e não compõem o total. As regras anteriores de correção são preservadas: garçom corrige antes do preparo; gerente/administrador podem intervir após o início, com motivo e histórico.

O resumo mantém produtos, desconto e total separados. Descontos/cortesias e troca de responsável têm áreas próprias. O fechamento começa com o total no primeiro recebimento e permite dividir em até três formas na interface existente. A conferência visual informa quanto falta ou excede; o servidor continua validando os valores. Ainda é somente um registro de pagamento externo, sem processar cartão.

Itens em fila, preparo ou prontos impedem fechar. Depois do fechamento, a mesa fica aguardando limpeza; a ação de liberar aparece em destaque. Fechamento e liberação continuam separados.

## Cozinha, recepção e funcionários

A cozinha mantém físico e delivery identificados e permite filtrar origem/etapa. A escolha fica na URL e é preservada após atualizar ou registrar preparo. As regras de liberação de delivery, versão e idempotência permanecem. Marcar pronto avisa o garçom; quem confirma a entrega à mesa continua sendo o garçom/gestor. A atualização automática de 15 segundos foi preservada.

A recepção separa aguardando chegada, fila presencial e grupo chamado. A data consultada aparece preenchida e é mantida ao registrar chegada/cancelar uma solicitação. Grupos sem reserva podem ser incluídos no dia atual. A chamada seleciona o primeiro grupo que cabe na capacidade da mesa. A disponibilidade das mesas é sempre atual, mesmo ao consultar reservas de outro dia.

A equipe tem cartões com cargo, acesso ativo/desativado/aguardando senha, busca e ações identificadas. A explicação dos cargos pode ser expandida. Convites, revogação de sessões, proteção dos gerentes e necessidade de transferir mesas antes de desativar o responsável permanecem.

## Uso de cupons

No cadastro/edição, **Uso por cliente** permite escolher:

1. **Uma vez por cliente**: reservas de uso e utilizações confirmadas ocupam o limite; cancelamento libera a utilização, conforme a regra já existente.
2. **Reutilizável até o vencimento**: não há limite individual; cada pedido continua permitindo um único cupom. Validade, pausa, valor mínimo, desconto máximo e limite total da campanha continuam valendo.

Cupons antigos mantêm o limite existente. Caso um cupom já tivesse limite personalizado maior que um, essa opção continua selecionável na edição, evitando convertê-lo silenciosamente.

A migration `CuponsReutilizaveis` permite nulo em `Cupons.LimitePorCliente`. Nulo representa ausência de limite individual. Não há recriação de tabela nem mudança das cotas existentes. A trava transacional do cupom e a conferência de validade/cota na confirmação do pedido foram mantidas. Mudar a regra altera a versão e invalida cotações antigas.

A lista de cupons fica visível diretamente; somente a explicação das regras é recolhível. Também foi corrigido o agrupamento anterior que colocava a tabela dentro da ajuda.

## Relatórios por período

O mesmo formulário de análise está disponível na visão geral e nos relatórios, com período, datas de comparação e canal. O período escolhido é exibido em descontos/promoções e em cupons. Atualizar a visão geral ou abrir o relatório detalhado preserva as datas selecionadas. Datas inválidas mostram erro; os contadores da operação atual continuam sendo obtidos com dados reais, sem apresentar zeros fictícios.

Os cupons têm uma seção separada: código, parceiro histórico, quantidade de utilizações confirmadas e desconto concedido no período. Um pedido com dez produtos e um cupom conta uma utilização. Cupons cadastrados até o fim do período também podem aparecer com zero usos. O histórico completo de cada cupom continua acessível e inclui as outras situações.

A contagem segue os pedidos elegíveis do relatório financeiro e a data do pedido em Brasília: pagamentos aprovados no delivery, ou dinheiro já entregue. Dinheiro antes da entrega não entra mesmo que tenha sido marcado aprovado. Cancelados, pagamentos recusados/reembolsados/em conferência e ajustes pendentes ficam fora. Reservas de utilização não são confundidas com utilizações confirmadas. Cupons são exclusivos do delivery; com filtro somente salão a seção explica essa distinção.

Nomes, códigos e descontos vêm das referências históricas. Quando há mais de um parceiro histórico no mesmo código, a seção indica isso; não atribui automaticamente o parceiro atual a vendas antigas. O extrato individual de usos continua sendo o detalhamento completo. Não foram modificadas as regras dos descontos do catálogo nem dos descontos presenciais.

## Ícones e acessibilidade

Carrinho, notificações e perfil compartilham a classe `navbar-icone`, área de 44 × 44 px, tamanho de ícone, cor padrão, hover dourado e efeito suave no próprio desenho. A área de toque e os contadores não pulam de posição. O foco de teclado tem o mesmo contorno nos três. Contadores mantêm fundo vinho e texto branco nos temas claro e escuro. A preferência por menos movimento remove as transformações.

Os IDs, destinos, abertura do carrinho, sino e painel da conta permanecem os mesmos. A padronização é visual, sem modificar esses fluxos do cliente.

## Validação e ativação

Foram aprovados 292 testes .NET e 18 testes JavaScript. Os testes incluem reutilização/uso único, expiração, limite total com cotação anterior, preservação de limites personalizados, contagem por código/período, autorização dos menus por cargo, visibilidade da lista de cupons, envio das datas na visão geral e preservação dos filtros após operações.

A conferência visual usa banco e servidor fictícios isolados. Nenhuma mesa, funcionário, cupom ou notificação promocional real é criada nessa verificação. Comentários anteriores são preservados.

Para ativar, aplicar a migration antes de executar/publicar a versão nova. Ao planejar uma reversão, cupons sem limite individual precisam ser considerados: versões antigas esperam um inteiro obrigatório. Não reverter a coluna convertendo campanhas reutilizáveis silenciosamente em limite zero.
