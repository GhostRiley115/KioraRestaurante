(() => {
    "use strict";
    document.querySelectorAll("[data-foto-produto]").forEach(foto => {
        const alternativa = () => {
            if (foto.dataset.fotoFalhou) return;
            foto.dataset.fotoFalhou = "true";
            foto.src = foto.dataset.fotoAlternativa;
        };
        foto.addEventListener("error", alternativa);
        if (foto.complete && foto.naturalWidth === 0) alternativa();
    });
})();
