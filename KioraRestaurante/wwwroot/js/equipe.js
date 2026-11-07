(() => {
    "use strict";
    const busca = document.getElementById("buscarProdutoSalao");
    busca?.addEventListener("input", () => {
        const texto = busca.value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("pt-BR");
        document.querySelectorAll("[data-produto-salao]").forEach(p => {
            p.hidden = !p.dataset.produtoSalao.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("pt-BR").includes(texto);
        });
    });
    const fila = document.getElementById("filaCozinha");
    if (!fila) return;
    const estado = document.querySelector("[data-cozinha-atualiza]");
    const versoes = [...document.querySelectorAll("[data-cozinha-item]")].map(i => `${i.dataset.chave}:${i.dataset.versao}`).sort().join("|");
    let consultando = false;
    async function conferir() {
        if (document.hidden || consultando) return;
        consultando = true;
        try {
            const resposta = await fetch(fila.dataset.url, {headers:{Accept:"application/json"},cache:"no-store"});
            if (!resposta.ok || resposta.redirected || !(resposta.headers.get("content-type") ?? "").includes("application/json")) return;
            const dados = await resposta.json();
            const atuais = dados.map(i=>`${i.itemSalaoId ?? i.itemDeliveryId}:${i.versao}`).sort().join("|");
            if (atuais !== versoes) {
                estado.textContent = "A fila mudou. Atualizando os itens e as etapas…";
                // Preserva uma ação em foco; a versão de cada item também é validada no servidor.
                if (!document.activeElement?.closest("form")) window.location.reload();
            }
        } catch { /* A atualização manual continua disponível durante falhas de conexão. */ }
        finally { consultando = false; }
    }
    setInterval(conferir,15000);
    document.addEventListener("visibilitychange",conferir);
})();
