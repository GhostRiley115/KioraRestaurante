// Controla apenas os campos de composição dos produtos.
document.querySelectorAll("[data-composicao]").forEach((bloco) => {
    const checkbox = bloco.querySelector("[data-eh-combo]");
    const campos = bloco.querySelector("[data-campos-composicao]");
    const lista = bloco.querySelector("[data-lista-componentes]");
    const modelo = bloco.querySelector("[data-modelo-componente]");
    const adicionar = bloco.querySelector("[data-adicionar-componente]");

    // As linhas renderizadas pelo servidor usam índices de 0 em diante.
    // Novas linhas sempre recebem um índice que ainda não foi usado.
    let proximoIndice = lista.querySelectorAll("[data-componente]").length;

    function adicionarLinha() {
        const indice = proximoIndice++;
        const fragmento = modelo.content.cloneNode(true);
        const linha = fragmento.querySelector("[data-componente]");

        const campoIndice = linha.querySelector("[data-indice]");
        campoIndice.name = "Combo.Componentes.Index";
        campoIndice.value = indice;

        const produto = linha.querySelector("[data-produto]");
        produto.name = `Combo.Componentes[${indice}].ProdutoId`;
        produto.id = `componente-produto-${indice}`;

        linha.querySelector("[data-label-produto]").htmlFor = produto.id;

        const quantidade = linha.querySelector("[data-quantidade]");
        quantidade.name = `Combo.Componentes[${indice}].Quantidade`;
        quantidade.id = `componente-quantidade-${indice}`;

        linha.querySelector("[data-label-quantidade]").htmlFor =
            quantidade.id;

        lista.append(fragmento);
    }

    function atualizarTipo() {
        const ehCombo = checkbox.checked;

        campos.hidden = !ehCombo;

        // Campos desabilitados não são enviados no formulário.
        // Produto comum não deve enviar componentes.
        campos.disabled = !ehCombo;

        checkbox.setAttribute("aria-expanded", String(ehCombo));

        if (ehCombo && lista.children.length === 0) {
            adicionarLinha();
        }
    }

    adicionar.addEventListener("click", () => {
        adicionarLinha();

        lista.lastElementChild
            ?.querySelector("select")
            ?.focus();
    });

    lista.addEventListener("click", (evento) => {
        const botao = evento.target.closest("[data-remover-componente]");

        if (!botao) {
            return;
        }

        botao.closest("[data-componente]").remove();
        adicionar.focus();
    });

    checkbox.addEventListener("change", atualizarTipo);

    atualizarTipo();
});