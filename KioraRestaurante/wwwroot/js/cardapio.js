/* =========================================================
   NAVEGAÇÃO DAS CATEGORIAS DO CARDÁPIO
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    // Seleciona todos os botões das categorias
    const botoesCategorias =
        document.querySelectorAll(".categoria-item");

    // Seleciona todas as seções de produtos
    const secoesCategorias =
        document.querySelectorAll(".categoria-produtos");


    /* =====================================================
       NAVEGAÇÃO DAS CATEGORIAS
       ===================================================== */

    if (
        botoesCategorias.length > 0 &&
        secoesCategorias.length > 0
    ) {

        /* =================================================
           QUANDO O USUÁRIO CLICA EM UMA CATEGORIA
           ================================================= */

        botoesCategorias.forEach(function (botao) {

            botao.addEventListener("click", function () {

                // Remove o estado ativo de todos os botões
                botoesCategorias.forEach(function (item) {
                    item.classList.remove("ativo");
                });

                // Ativa o botão clicado
                botao.classList.add("ativo");

            });

        });


        /* =================================================
           IDENTIFICA A CATEGORIA DURANTE A ROLAGEM
           ================================================= */

        const observer = new IntersectionObserver(
            function (entradas) {

                entradas.forEach(function (entrada) {

                    if (entrada.isIntersecting) {

                        // ID da seção atualmente visível
                        const idCategoria =
                            entrada.target.id;

                        // Procura o botão correspondente
                        botoesCategorias.forEach(function (botao) {

                            const href =
                                botao.getAttribute("href");

                            // Remove o estado ativo
                            botao.classList.remove("ativo");

                            // Ativa o botão correspondente
                            // à seção atualmente visível
                            if (
                                href === "#" + idCategoria
                            ) {
                                botao.classList.add("ativo");
                            }

                        });

                    }

                });

            },
            {
                // Define quando a categoria será considerada visível
                threshold: 0.25,

                // Ajusta a área considerada pelo observer
                rootMargin: "-120px 0px -50% 0px"
            }
        );


        /* =================================================
           OBSERVA TODAS AS CATEGORIAS
           ================================================= */

        secoesCategorias.forEach(function (secao) {
            observer.observe(secao);
        });

    }


    /* =========================================================
       BOTÕES "ADICIONAR"
       ========================================================= */

    // Seleciona todos os botões de adicionar produto
    const botoesAdicionar =
        document.querySelectorAll(".btn-adicionar-produto");


    /* =====================================================
       CLIQUE NO BOTÃO "ADICIONAR"
       ===================================================== */

    botoesAdicionar.forEach(function (botao) {

        botao.addEventListener("click", function () {

            // Evita múltiplos cliques enquanto a animação acontece
            if (botao.classList.contains("animando")) {
                return;
            }

            // Guarda o conteúdo original do botão
            const conteudoOriginal =
                botao.innerHTML;

            // Adiciona as classes utilizadas pelo CSS
            botao.classList.add("animando");
            botao.classList.add("adicionado");

            // Altera temporariamente o conteúdo
            botao.innerHTML = `
                <i class="bi bi-check-lg"></i>
                <span>Adicionado</span>
            `;


            /* =============================================
               RESTAURA O BOTÃO APÓS A ANIMAÇÃO
               ============================================= */

            setTimeout(function () {

                botao.classList.remove("adicionado");
                botao.classList.remove("animando");

                // Volta ao conteúdo original
                botao.innerHTML =
                    conteudoOriginal;

            }, 1800);

        });

    });

});