# Revisão visual, CSS e JavaScript — 09/11/2026

## Resultado

A identidade visual aprovada foi mantida: vinho para ações principais, dourado para destaques, superfícies neutras, títulos com Playfair Display e texto de leitura com a fonte do site. A revisão corrigiu diferenças entre componentes e entre os temas claro e escuro, retirou código comprovadamente aposentado e organizou o carregamento de estilos.

O projeto continua usando suas bibliotecas atuais. Não há novas dependências de produção, alteração de banco ou mudança nas regras de pagamento, entrega e permissões.

## O que foi corrigido na apresentação

| Área | Problema encontrado | Ajuste |
|---|---|---|
| Navbar | Estilos antigos de carrinho e perfil concorriam com o componente atual dos três ícones. | Os ícones passam a depender do componente `navbar-acoes.css`; tamanho, foco, contador e comportamento visual ficam no mesmo lugar. |
| Conta e formulários | Mensagens de perfil/senha usavam cores inseridas pelo JavaScript. Campos e ícones não acompanhavam totalmente o tema. | Mensagens usam classes semânticas existentes; campos, foco e ícones acompanham a paleta global. |
| Modais no tema claro | O filtro branco do botão de fechar tornava o X pouco visível. | O filtro de fechamento passa a depender do tema, incluindo perfil e alteração de senha. |
| Cardápio e carrinho | Algumas etiquetas vinho herdavam texto escuro no tema claro. | Etiquetas de combo/desconto, botões e indicador animado de adição têm contraste definido pelo próprio componente. |
| Administração | Regras antigas e novas de cards, navegação e equipe estavam acumuladas. | Declarações foram consolidadas; ações destrutivas recebem tratamento visual próprio, e filtros preservam o estado selecionado. |
| Cozinha | Ao filtrar apenas salão ou apenas delivery, a coluna visível ocupava metade da área. | A origem selecionada usa a largura disponível. Com ambas as origens, a separação em colunas permanece. |
| Detalhes do pedido | Foto, nome longo e subtotal podiam disputar espaço no celular. | Os itens quebram de linha de forma legível, sem empurrar a página para os lados. |
| Pagamento | O botão ainda dependia da aparência padrão azul do Bootstrap. | Usa o mesmo componente de ação dos demais passos do pedido. |
| Documentos | Termos e política tinham superfícies fixas e texto pequeno no celular. | Acompanham os dois temas, com leitura de 16 px no celular e borda alinhada aos demais cards. |
| Galeria | A exibição do visualizador era controlada simultaneamente por classes, opacidade e CSS global. | O estado nativo do `dialog` controla a abertura. Foco, Escape e retorno ao item de origem foram conferidos. Controles têm pelo menos 44 px, e legendas em telas de toque não dependem de passar o mouse. |
| Sobre e rodapé | Havia declarações repetidas e bordas com cores fixas. | Regras foram reunidas e as divisórias usam a paleta. A assinatura e os links da Jujuba's Dev foram preservados. |
| Erro e acesso negado | A tela de erro ainda era o modelo técnico em inglês. | Recebeu texto em português, identidade Kiora, caminho de retorno e código de suporte. Em uma falha na compra, orienta conferir os pedidos antes de repetir. |

## Organização e limpeza do CSS

- O sino e seu painel foram extraídos de `pedido.css` para `notificacoes.css`, carregado globalmente antes do componente da navbar.
- `cardapio.css` passa a carregar na página de cardápio; `documentos.css`, nos termos e na política; `produto-admin.css`, nas telas administrativas de produtos.
- `pedido.css` permanece global porque seus botões, formulários e resumos também são usados em endereços, reservas, convites, cupons e comunicados.
- Foram retirados os seletores do dropdown antigo de usuário, cards antigos de cardápio/carrinho, layout independente antigo da equipe e detalhes administrativos substituídos. As referências foram pesquisadas em Views, JavaScript e C#, incluindo geração dinâmica de classes.
- Foram consolidadas declarações repetidas e substituídas correções globais de cor com `!important` por regras no componente responsável.
- O antigo CSS isolado `_Layout.cshtml.css`, originado do modelo padrão e sem uso necessário, foi retirado junto de sua referência ao pacote de estilos.
- `estados.css` reúne a apresentação das telas de erro e acesso negado.
- A página inglesa `Privacy.cshtml` do modelo padrão foi retirada. A rota `Home/Privacy` continua disponível e redireciona para a política em português.

Nem toda sobrescrita é um defeito. Foram mantidas as variantes de tema, os estados de foco/seleção, as adaptações de tela pequena, os fallbacks `vh`/`dvh` e os ajustes que neutralizam o posicionamento inline do Popper no painel móvel de notificações. O atributo `hidden` continua protegido onde componentes usam `display: grid` ou `flex`.

Os arquivos de Bootstrap, ícones, jQuery e validação não foram cortados com base apenas no HTML inicial: contêm estados e componentes gerados em tempo de execução.

## JavaScript

| Arquivo | Mudança |
|---|---|
| `conta.js` | Usa classes de mensagens em vez de doze atribuições de cores inline; retira busca de elemento do menu aposentado e logs de respostas completas de autenticação/desativação. |
| `checkout.js` | Confere novamente a validade da cotação no envio, para o caso de o cliente permanecer muito tempo na página; impede envio quando a validação do formulário falha. O servidor continua sendo responsável pela validação definitiva. |
| `reservas.js` | Uma resposta antiga não substitui os horários de uma consulta mais recente, mesmo se a leitura assíncrona do JSON terminar depois. Reservas canceladas ou expiradas deixam de mostrar prazo ativo. |
| `galeria.js` | Só inicializa o visualizador quando seus elementos necessários existem. |

Foram retirados dois scripts aposentados:

- `wwwroot/js/cardapio.js`: nenhuma View o carregava; continha filtro e simulação de carrinho anteriores à implementação atual.
- `wwwroot/js/site.js`: continha apenas comentários, sem comportamento executável, mas ainda era solicitado em todas as páginas.

O `importmap` vazio do layout também foi retirado. O carrinho real, os filtros atuais, os gráficos, as notificações e os fluxos de autenticação continuam com seus scripts próprios.

## Comentários preservados

Foram conferidos **2.774 comentários originais** dos arquivos alterados ou retirados, sem comentário ausente. Os comentários dos arquivos aposentados estão preservados em:

- [Comentários do JavaScript aposentado](LEGADO-FRONTEND-COMENTARIOS.md)
- [Comentários do layout aposentado](LEGADO-LAYOUT-COMENTARIOS.md)

Comentários movidos entre arquivos de CSS também foram mantidos. Por isso alguns blocos históricos permanecem documentados mesmo após a retirada das regras antigas.

## Validação realizada

| Verificação | Resultado |
|---|---|
| Compilação .NET | Sem erros e sem avisos de compilação. |
| Testes .NET | 324 aprovados. |
| Testes JavaScript | 25 aprovados, incluindo sete novas regressões de interface. |
| Sintaxe dos scripts próprios restantes | 16 arquivos válidos. |
| Sintaxe dos CSS próprios | 19 arquivos válidos. |
| Páginas e estados por perfil | 69 casos, sem falha inesperada: 68 respostas 200 e uma resposta 403 de acesso negado, prevista. |
| Recursos locais e links internos | 59 recursos e 280 links internos, sem falha. |
| Responsividade e temas | 552 combinações: 69 casos × larguras de 320, 390, 768 e 1366 px × temas claro e escuro. |
| Layout e imagens nessas combinações | Sem transbordamento lateral ou imagens quebradas detectados. |
| Contraste automatizado de texto | Sem falha nos textos medidos sobre superfícies opacas, usando os limiares de 4,5:1 para texto comum e 3:1 para texto grande. Áreas sobre fotografias foram conferidas visualmente. |

A cobertura inclui as três homes; cardápio e coleções; sobre, galeria, termos e política; conta e endereços; checkout, pagamento e detalhes/histórico; reservas; notificações e cupons; administração, relatórios, produtos, descontos, usuários, comunicados e equipe; salão, comandas, recepção, cozinha e configuração de mesas. Foram incluídos estados cancelados, concluídos, de erro e de acesso negado.

Também foram exercitados no navegador: adicionar produto como visitante e abrir o carrinho; painel da conta e troca de tema; login/cadastro; contato; filtros e visualizador da galeria; modal de perfil; painel móvel de notificações; filtros da cozinha. As telas exclusivas de retorno de convite e prévia de comunicado foram renderizadas com modelos fictícios em um ambiente de conferência.

As verificações usaram banco temporário e dados fictícios, sem finalizar pagamentos, enviar convites ou alterar pedidos do ambiente compartilhado. A medição de contraste e a varredura de páginas complementam a conferência visual; não representam a execução de todas as combinações possíveis de dados e ações do sistema.

## Efeito no tamanho e manutenção

O total dos arquivos próprios diretamente em `wwwroot/css` caiu de **282.743 para 275.994 bytes**; em `wwwroot/js`, de **155.271 para 150.345 bytes**. São **11.675 bytes de código a menos**, já considerando os novos CSS, sem apagar os comentários didáticos. O carregamento específico de estilos também evita solicitar arquivos de páginas que o visitante não abriu.

Para manter essa organização, acrescente estilos no componente que os utiliza, use as variáveis de tema e confira tanto o estado claro quanto o escuro. Antes de remover uma classe, pesquise também os arquivos JavaScript, os auxiliares C# e as classes montadas dinamicamente.
