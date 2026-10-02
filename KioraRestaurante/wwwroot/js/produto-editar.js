// EDIÇÃO DE PRODUTO: prévia da foto e proteção contra envio repetido.
(() => {
    "use strict";

    const formulario = document.getElementById("formEditarProduto");
    if (!formulario) return;

    const foto = document.getElementById("Foto");
    const preview = document.getElementById("previewProdutoEdicao");
    const botao = document.getElementById("salvarProduto");

    const imagemAtual = preview.getAttribute("src");
    const bloqueadoInicialmente = botao.disabled;

    let urlTemporaria = null;

    function restaurarImagemAtual() {
        if (imagemAtual) {
            preview.src = imagemAtual;
            preview.hidden = false;
        } else {
            preview.removeAttribute("src");
            preview.hidden = true;
        }
    }

    foto.addEventListener("change", () => {
        // Libera a prévia anterior para não acumular memória.
        if (urlTemporaria) {
            URL.revokeObjectURL(urlTemporaria);
            urlTemporaria = null;
        }

        foto.setCustomValidity("");
        restaurarImagemAtual();

        const arquivo = foto.files[0];

        // Sem uma nova seleção, continua mostrando a foto cadastrada.
        if (!arquivo) return;

        if (arquivo.size === 0) {
            foto.setCustomValidity("A foto está vazia.");
        } else if (arquivo.size > 5 * 1024 * 1024) {
            foto.setCustomValidity("A foto deve ter no máximo 5 MB.");
        } else if (
            !["image/jpeg", "image/png", "image/webp"].includes(arquivo.type)
        ) {
            foto.setCustomValidity("Selecione uma foto JPG, PNG ou WebP.");
        }

        if (!foto.reportValidity()) return;

        urlTemporaria = URL.createObjectURL(arquivo);
        preview.src = urlTemporaria;
        preview.hidden = false;
    });

    formulario.addEventListener("submit", evento => {
        if (!formulario.reportValidity() || !$(formulario).valid()) {
            evento.preventDefault();
            return;
        }

        botao.disabled = true;
        botao.textContent = "Salvando…";
    });

    window.addEventListener("pageshow", () => {
        botao.disabled = bloqueadoInicialmente;
        botao.textContent = "Salvar alterações";
    });
})();