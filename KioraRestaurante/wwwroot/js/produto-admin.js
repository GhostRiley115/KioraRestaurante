// ADMIN / PRODUTOS: prévia da foto e bloqueio de envio repetido pela interface.
(() => {
    "use strict";
    const formulario = document.getElementById("formCadastrarProduto");
    if (!formulario) return;
    const foto = document.getElementById("Foto");
    const preview = document.getElementById("previewProduto");
    const botao = document.getElementById("cadastrarProduto");
    const bloqueadoInicialmente = botao.disabled;
    let urlTemporaria = null;

    // Validação no navegador ajuda o usuário; o serviço também valida o arquivo.
    foto.addEventListener("change", () => {
        if (urlTemporaria) URL.revokeObjectURL(urlTemporaria);
        urlTemporaria = null;
        preview.hidden = true;
        preview.removeAttribute("src");
        foto.setCustomValidity("");
        const arquivo = foto.files[0];
        if (!arquivo) return;
        if (arquivo.size > 5 * 1024 * 1024) {
            foto.setCustomValidity("A foto deve ter no máximo 5 MB.");
        } else if (!["image/jpeg", "image/png", "image/webp"].includes(arquivo.type)) {
            foto.setCustomValidity("Selecione uma foto JPG, PNG ou WebP.");
        }
        if (!foto.reportValidity()) return;
        urlTemporaria = URL.createObjectURL(arquivo);
        preview.src = urlTemporaria;
        preview.hidden = false;
    });

    // O formulário continua usando POST normal, com validação e antiforgery.
    formulario.addEventListener("submit", () => {
        if (!$(formulario).valid()) return;
        botao.disabled = true;
        botao.textContent = "Cadastrando…";
    });
    window.addEventListener("pageshow", () => {
        botao.disabled = bloqueadoInicialmente;
        botao.textContent = "Cadastrar produto";
    });
})();
