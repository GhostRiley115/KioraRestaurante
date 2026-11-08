// A orientação acompanha o tipo; o servidor continua responsável por validar e calcular o desconto.
(() => {
    "use strict";
    const form = document.querySelector("[data-cupom-form]");
    const tipo = form?.querySelector('[name="Tipo"]');
    const ajuda = form?.querySelector("#ajudaValor");
    if (!tipo || !ajuda) return;
    const atualizar = () => {
        ajuda.textContent = tipo.value === "0"
            ? "Em porcentagem: digite 10 para dar 10%. O máximo é 100%."
            : "Em reais: digite 15,90 para descontar R$ 15,90 dos produtos.";
    };
    tipo.addEventListener("change", atualizar);
    atualizar();
})();
