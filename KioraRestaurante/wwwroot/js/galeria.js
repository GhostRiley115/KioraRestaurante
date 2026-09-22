/* =========================================================
   GALERIA - KIORA
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {


    /* =====================================================
       ELEMENTOS DA GALERIA
       ===================================================== */

    // Seleciona os itens da grade e os botões de categoria.
    const itensGaleria =
        document.querySelectorAll(".galeria-item");

    const botoesCategorias =
        document.querySelectorAll(".categoria-galeria");


    /* =====================================================
       ELEMENTOS DO LIGHTBOX
       ===================================================== */

    const lightbox =
        document.getElementById("lightboxGaleria");

    const lightboxImagem =
        document.getElementById("lightboxImagem");

    const lightboxTitulo =
        document.getElementById("lightboxTitulo");

    const lightboxCategoria =
        document.getElementById("lightboxCategoria");

    const botaoFechar =
        document.getElementById("lightboxFechar");

    const botaoAnterior =
        document.getElementById("lightboxAnterior");

    const botaoProximo =
        document.getElementById("lightboxProximo");

    const fundoLightbox =
        document.querySelector(".lightbox-fundo");


    /* =====================================================
       CONTROLE DAS IMAGENS
       ===================================================== */

    /*
     * Lista das imagens atualmente disponíveis
     * para navegação no Lightbox.
     */
    let imagensGaleria =
        Array.from(itensGaleria);

    // Índice da imagem atualmente aberta.
    let indiceAtual = 0;


    /* =====================================================
       FILTRO DAS CATEGORIAS
       ===================================================== */

    botoesCategorias.forEach(function (botao) {

        botao.addEventListener("click", function () {

            /*
             * Identifica a categoria escolhida através
             * do atributo data-categoria do botão.
             */
            const categoriaSelecionada =
                this.getAttribute("data-categoria");


            /* -------------------------------------------------
               ATUALIZA O BOTÃO ATIVO
               ------------------------------------------------- */

            botoesCategorias.forEach(function (item) {

                item.classList.remove("ativo");

            });

            this.classList.add("ativo");


            /* -------------------------------------------------
               MOSTRA OU ESCONDE AS IMAGENS
               ------------------------------------------------- */

            itensGaleria.forEach(function (item) {

                const categoriaItem =
                    item.getAttribute("data-categoria");


                /*
                 * "Todos" exibe todas as imagens.
                 * Nas demais categorias, somente as imagens
                 * correspondentes ficam visíveis.
                 */
                if (
                    categoriaSelecionada === "todos" ||
                    categoriaItem === categoriaSelecionada
                ) {

                    item.style.display = "";

                }
                else {

                    item.style.display = "none";

                }

            });


            /* -------------------------------------------------
               ATUALIZA A LISTA DO LIGHTBOX
               ------------------------------------------------- */

            /*
             * O Lightbox passa a navegar somente pelas
             * imagens da categoria selecionada.
             */
            imagensGaleria =
                Array.from(itensGaleria).filter(function (item) {

                    if (categoriaSelecionada === "todos") {

                        return true;

                    }

                    return item.getAttribute("data-categoria")
                        === categoriaSelecionada;

                });


            // Reinicia a navegação do Lightbox.
            indiceAtual = 0;


            /* -------------------------------------------------
               ROLAGEM ATÉ A GRADE DE FOTOS
               ------------------------------------------------- */

            const secaoFotos =
                document.querySelector(".galeria-fotos");

            if (secaoFotos) {

                secaoFotos.scrollIntoView({
                    behavior: "smooth",
                    block: "start"
                });

            }

        });

    });


    /* =====================================================
       ABRIR LIGHTBOX
       ===================================================== */

    function abrirLightbox(indice) {

        indiceAtual = indice;

        const item =
            imagensGaleria[indiceAtual];


        // Segurança caso não exista uma imagem.
        if (!item) {

            return;

        }


        /*
         * Recupera as informações armazenadas no HTML
         * através dos atributos data-imagem, data-titulo
         * e data-categoria.
         */
        const caminhoImagem =
            item.dataset.imagem;

        const titulo =
            item.dataset.titulo || "";

        const categoria =
            item.dataset.categoria || "";


        /* -------------------------------------------------
           PREENCHE O LIGHTBOX
           ------------------------------------------------- */

        lightboxImagem.src =
            caminhoImagem;

        lightboxImagem.alt =
            titulo;

        lightboxTitulo.textContent =
            titulo;

        lightboxCategoria.textContent =
            categoria.toUpperCase();


        // Exibe o Lightbox.
        lightbox.classList.add("ativo");


        // Impede o scroll da página enquanto estiver aberto.
        document.body.style.overflow = "hidden";

    }


    /* =====================================================
       FECHAR LIGHTBOX
       ===================================================== */

    function fecharLightbox() {

        // Remove a classe que exibe o Lightbox.
        lightbox.classList.remove("ativo");

        // Restaura o scroll normal da página.
        document.body.style.overflow = "";

        // Limpa a imagem carregada.
        lightboxImagem.src = "";

    }


    /* =====================================================
       PRÓXIMA IMAGEM
       ===================================================== */

    function proximaImagem() {

        indiceAtual++;

        /*
         * Ao chegar ao final da lista,
         * retorna para a primeira imagem.
         */
        if (indiceAtual >= imagensGaleria.length) {

            indiceAtual = 0;

        }

        abrirLightbox(indiceAtual);

    }


    /* =====================================================
       IMAGEM ANTERIOR
       ===================================================== */

    function imagemAnterior() {

        indiceAtual--;

        /*
         * Ao passar da primeira imagem,
         * retorna para a última.
         */
        if (indiceAtual < 0) {

            indiceAtual =
                imagensGaleria.length - 1;

        }

        abrirLightbox(indiceAtual);

    }


    /* =====================================================
       CLIQUE NAS IMAGENS
       ===================================================== */

    itensGaleria.forEach(function (item) {

        item.addEventListener("click", function () {

            /*
             * Imagens escondidas pelo filtro não podem
             * ser abertas.
             */
            if (item.style.display === "none") {

                return;

            }


            /*
             * Localiza a imagem dentro da lista atual
             * utilizada pelo Lightbox.
             */
            const indice =
                imagensGaleria.indexOf(item);


            if (indice === -1) {

                return;

            }


            abrirLightbox(indice);

        });

    });


    /* =====================================================
       CONTROLES DO LIGHTBOX
       ===================================================== */

    // Fechar pelo botão "X".
    botaoFechar.addEventListener("click", function () {

        fecharLightbox();

    });


    // Próxima imagem.
    botaoProximo.addEventListener("click", function () {

        proximaImagem();

    });


    // Imagem anterior.
    botaoAnterior.addEventListener("click", function () {

        imagemAnterior();

    });


    // Clicar no fundo escuro também fecha o Lightbox.
    fundoLightbox.addEventListener("click", function () {

        fecharLightbox();

    });


    /* =====================================================
       CONTROLES PELO TECLADO
       ===================================================== */

    document.addEventListener("keydown", function (evento) {

        // Ignora os comandos quando o Lightbox está fechado.
        if (!lightbox.classList.contains("ativo")) {

            return;

        }


        // ESC fecha o Lightbox.
        if (evento.key === "Escape") {

            fecharLightbox();

        }


        // Seta direita: próxima imagem.
        if (evento.key === "ArrowRight") {

            proximaImagem();

        }


        // Seta esquerda: imagem anterior.
        if (evento.key === "ArrowLeft") {

            imagemAnterior();

        }

    });

});