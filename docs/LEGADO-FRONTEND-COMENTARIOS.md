# Comentários preservados dos scripts aposentados

Os arquivos abaixo deixaram de ser distribuídos em `wwwroot/js`. `cardapio.js` não era carregado pela view atual e apenas simulava adição visual, sem gravar no carrinho. `site.js` continha somente comentários e gerava uma requisição sem executar comportamento. O carrinho real continua em `carrinho.js`, e os fluxos de conta em `conta.js`.

Todos os comentários originais desses dois arquivos estão preservados literalmente abaixo. São registro histórico, não instruções para reativar a implementação antiga.

## cardapio.js

```javascript
/* =========================================================
   NAVEGAÇÃO DAS CATEGORIAS DO CARDÁPIO
   ========================================================= */

// Seleciona todos os botões das categorias

// Seleciona todas as seções de produtos

/* =====================================================
       NAVEGAÇÃO DAS CATEGORIAS
       ===================================================== */

/* =================================================
           QUANDO O USUÁRIO CLICA EM UMA CATEGORIA
           ================================================= */

// Remove o estado ativo de todos os botões

// Ativa o botão clicado

/* =================================================
           IDENTIFICA A CATEGORIA DURANTE A ROLAGEM
           ================================================= */

// ID da seção atualmente visível

// Procura o botão correspondente

// Remove o estado ativo

// Ativa o botão correspondente

// à seção atualmente visível

// Define quando a categoria será considerada visível

// Ajusta a área considerada pelo observer

/* =================================================
           OBSERVA TODAS AS CATEGORIAS
           ================================================= */

/* =========================================================
       BOTÕES "ADICIONAR"
       ========================================================= */

// Seleciona todos os botões de adicionar produto

/* =====================================================
       CLIQUE NO BOTÃO "ADICIONAR"
       ===================================================== */

// Evita múltiplos cliques enquanto a animação acontece

// Guarda o conteúdo original do botão

// Adiciona as classes utilizadas pelo CSS

// Altera temporariamente o conteúdo

/* =============================================
               RESTAURA O BOTÃO APÓS A ANIMAÇÃO
               ============================================= */

// Volta ao conteúdo original
```

## site.js

```javascript
// ================================================================

// COMPORTAMENTOS GERAIS DO SITE

// ================================================================

// Login, cadastro, perfil e senhas estão em conta.js.

// O carrinho permanece em carrinho.js.

// Adicione aqui apenas comportamentos compartilhados que não sejam da conta.
```
