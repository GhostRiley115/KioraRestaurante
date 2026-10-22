// Executado no head para escolher o tema antes de pintar a página.
(() => {
    "use strict";
    const chave = "kiora.tema";
    const opcoes = ["sistema", "claro", "escuro"];
    const sistema = window.matchMedia("(prefers-color-scheme: dark)");
    let escolha = "sistema";
    try { const salva = localStorage.getItem(chave); if (opcoes.includes(salva)) escolha = salva; } catch { /* O tema funciona mesmo sem armazenamento disponível. */ }
    function aplicar() {
        const escuro = escolha === "escuro" || (escolha === "sistema" && sistema.matches);
        document.documentElement.dataset.tema = escuro ? "escuro" : "claro";
        document.documentElement.dataset.bsTheme = escuro ? "dark" : "light";
        document.querySelectorAll("[data-seletor-tema]").forEach(select => { select.value = escolha; });
    }
    aplicar();
    sistema.addEventListener("change", () => { if (escolha === "sistema") aplicar(); });
    window.addEventListener("storage", event => {
        if (event.key !== chave && event.key !== null) return;
        escolha = opcoes.includes(event.newValue) ? event.newValue : "sistema";
        aplicar();
    });
    document.addEventListener("DOMContentLoaded", () => {
        aplicar();
        document.querySelectorAll("[data-seletor-tema]").forEach(select => {
            select.addEventListener("change", () => {
                escolha = opcoes.includes(select.value) ? select.value : "sistema";
                try { localStorage.setItem(chave, escolha); } catch { /* Mantém a escolha nesta página. */ }
                aplicar();
            });
        });
    });
})();
