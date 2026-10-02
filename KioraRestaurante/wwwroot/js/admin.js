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
