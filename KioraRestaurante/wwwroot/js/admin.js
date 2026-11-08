// ADMIN: confirma alterações de acesso e evita cliques repetidos no mesmo formulário.
(() => {
    "use strict";
    document.querySelectorAll("form[data-confirmar]").forEach(formulario => {
        formulario.addEventListener("submit", evento => {
            if (!window.confirm(formulario.dataset.confirmar)) {
                evento.preventDefault();
                return;
            }
            const botao = formulario.querySelector('button[type="submit"]');
            botao.disabled = true;
            botao.textContent = "Salvando…";
        });
    });
    // Ao voltar pelo histórico do navegador, restaura os botões sem perder os filtros.
    window.addEventListener("pageshow", evento => {
        if (evento.persisted) window.location.reload();
    });
})();

// As barras também respondem a foco e toque; os valores continuam disponíveis sem JavaScript.
(() => {
    "use strict";
    document.querySelectorAll("[data-grafico-detalhe]").forEach(barra => {
        const mostrar = () => {
            const leitura = barra.closest(".admin-grafico-box")?.querySelector("output");
            if (leitura) leitura.textContent = barra.dataset.graficoDetalhe;
        };
        barra.addEventListener("focus", mostrar);
        barra.addEventListener("click", mostrar);
        barra.addEventListener("mouseenter", mostrar);
    });
    const atual = new URL(window.location.href);
    document.querySelectorAll(".admin-nav a").forEach(link => {
        if (new URL(link.href).pathname.toLowerCase() === atual.pathname.toLowerCase()) link.setAttribute("aria-current", "page");
    });
    // O preview é apenas visual; a validação e o cálculo definitivos permanecem no servidor.
    document.querySelectorAll("[data-desconto-preview]").forEach(form => {
        const campo = form.querySelector('[name="PercentualTexto"]');
        const saida = form.querySelector("[data-preco-preview]");
        if (!campo || !saida) return;
        const atualizar = () => {
            const texto = campo.value.trim().replace(",", ".");
            const percentual = Number(texto);
            if (!/^\d{1,2}(\.\d{1,2})?$/.test(texto) || percentual < 0 || percentual > 90) {
                saida.textContent = "Informe um percentual entre 0 e 90.";
                return;
            }
            const centavos = Math.round(Number(form.dataset.precoOriginal) * (100 - percentual));
            saida.textContent = (centavos / 100).toLocaleString("pt-BR", { style:"currency", currency:"BRL" });
        };
        campo.addEventListener("input", atualizar);
        atualizar();
    });
})();

// No celular, a navegação começa recolhida para dar espaço à tarefa atual.
(() => {
    const menu = document.querySelector(".admin-menu");
    if (!menu) return;
    const largura = window.matchMedia("(min-width: 992px)");
    menu.open = largura.matches;
    largura.addEventListener("change", evento => { menu.open = evento.matches; });
})();
