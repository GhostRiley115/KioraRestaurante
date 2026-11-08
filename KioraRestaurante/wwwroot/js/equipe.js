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

// Filtros locais e tarefas de atendimento não alteram as permissões nem os valores do servidor.
(() => {
    "use strict";
    const normalizar = valor => String(valor ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("pt-BR");
    const mesas = [...document.querySelectorAll("[data-mesa]")];
    const buscaMesa = document.querySelector("[data-buscar-mesas]");
    let estadoMesa = "todas";
    const filtrarMesas = () => {
        let visiveis = 0;
        mesas.forEach(m => {
            m.hidden = !(estadoMesa === "todas" || estadoMesa === "minhas" && m.dataset.minha === "true" || m.dataset.estado === estadoMesa)
                || !normalizar(m.dataset.busca).includes(normalizar(buscaMesa?.value));
            if (!m.hidden) visiveis++;
        });
        const vazio = document.querySelector("[data-mesas-vazias]");
        if (vazio) vazio.hidden = visiveis > 0;
    };
    buscaMesa?.addEventListener("input", filtrarMesas);
    document.querySelectorAll("[data-filtro-mesa]").forEach(b => b.addEventListener("click", () => {
        estadoMesa = b.dataset.filtroMesa;
        document.querySelectorAll("[data-filtro-mesa]").forEach(x => x.setAttribute("aria-pressed", String(x === b)));
        filtrarMesas();
    }));
    document.querySelectorAll("[data-grupo-acomodar]").forEach(campo => {
        const atualizar = () => { const manual = campo.closest("form").querySelector("[data-grupo-manual]"); if (manual) manual.hidden = campo.value !== ""; };
        campo.addEventListener("change", atualizar); atualizar();
    });
    const buscaEquipe = document.querySelector("[data-buscar-equipe]");
    buscaEquipe?.addEventListener("input", () => {
        let visiveis = 0;
        document.querySelectorAll("[data-funcionario]").forEach(f => { f.hidden = !normalizar(f.dataset.busca).includes(normalizar(buscaEquipe.value)); if (!f.hidden) visiveis++; });
        document.querySelector("[data-equipe-vazia]").hidden = visiveis > 0;
    });
    const abas = [...document.querySelectorAll("[data-comanda-aba]")];
    const mostrarAba = nome => {
        abas.forEach(b => b.setAttribute("aria-pressed", String(b.dataset.comandaAba === nome)));
        document.querySelectorAll("[data-comanda-painel]").forEach(p => { p.hidden = p.dataset.comandaPainel !== nome; });
    };
    abas.forEach(b => b.addEventListener("click", () => mostrarAba(b.dataset.comandaAba)));
    if (abas.length) mostrarAba(document.querySelector(".comanda-item") ? "consumo" : "cardapio");
    const buscaProduto = document.getElementById("buscarProdutoSalao");
    buscaProduto?.addEventListener("input", () => {
        document.querySelectorAll("[data-categoria-salao]").forEach(c => { c.hidden = [...c.querySelectorAll("[data-produto-salao]")].every(p => p.hidden); });
        const vazio = document.querySelector("[data-produtos-vazios]");
        if (vazio) vazio.hidden = [...document.querySelectorAll("[data-produto-salao]")].some(p => !p.hidden);
    });
    const dinheiro = centavos => (centavos / 100).toLocaleString("pt-BR", { style:"currency",currency:"BRL" });
    const centavos = texto => { const t = texto.trim().replace(",", "."); if (!/^\d{1,8}(\.\d{1,2})?$/.test(t)) return null; const [reais, cents = ""] = t.split("."); return Number(reais) * 100 + Number(cents.padEnd(2,"0")); };
    document.querySelectorAll("[data-fechamento-comanda]").forEach(form => {
        const atualizar = () => {
            const valores = [...form.querySelectorAll("[data-valor-recebido]")].map(e => centavos(e.value));
            const saida = form.querySelector("[data-conferencia-recebimento]");
            if (valores.some(v => v === null)) { saida.textContent = "Confira os valores recebidos. Use até duas casas decimais."; return; }
            const soma = valores.reduce((a,b) => a+b,0); const total = Math.round(Number(form.dataset.total)*100); const falta = total-soma;
            saida.textContent = falta === 0 ? `Soma conferida: ${dinheiro(soma)}.` : falta > 0 ? `Registrado ${dinheiro(soma)}. Faltam ${dinheiro(falta)}.` : `Os recebimentos ultrapassam a conta em ${dinheiro(-falta)}.`;
        };
        form.addEventListener("input", atualizar); atualizar();
    });
    const itens = [...document.querySelectorAll("[data-cozinha-item]")];
    if (!document.getElementById("filaCozinha")) return;
    const url = new URL(window.location.href);
    let origem = ["salao","delivery"].includes(url.searchParams.get("origem")) ? url.searchParams.get("origem") : "todas";
    let etapa = ["NaFila","EmPreparo","Pronto"].includes(url.searchParams.get("etapa")) ? url.searchParams.get("etapa") : "todas";
    const filtrarFila = () => {
        itens.forEach(i => { i.hidden = !(origem === "todas" || i.dataset.origem === origem) || !(etapa === "todas" || i.dataset.etapa === etapa); });
        document.querySelectorAll("[data-fila-origem]").forEach(b => b.setAttribute("aria-pressed",String(b.dataset.filaOrigem === origem)));
        document.querySelectorAll("[data-fila-etapa]").forEach(b => b.setAttribute("aria-pressed",String(b.dataset.filaEtapa === etapa)));
        document.querySelectorAll("[data-cozinha-coluna]").forEach(c => { c.hidden = origem !== "todas" && c.dataset.cozinhaColuna !== origem; });
        document.querySelector("[data-fila-vazia]").hidden = itens.some(i => !i.hidden);
        url.searchParams.set("origem",origem); url.searchParams.set("etapa",etapa);
        window.history.replaceState(null,"",url);
        const atualizar = document.querySelector("[data-atualizar-cozinha]");
        if (atualizar) atualizar.href = url.href;
        document.querySelectorAll('[name="origemFiltro"]').forEach(c => { c.value = origem; });
        document.querySelectorAll('[name="etapaFiltro"]').forEach(c => { c.value = etapa; });
    };
    document.querySelectorAll("[data-fila-origem]").forEach(b => b.addEventListener("click", () => { origem = b.dataset.filaOrigem; filtrarFila(); }));
    document.querySelectorAll("[data-fila-etapa]").forEach(b => b.addEventListener("click", () => { etapa = b.dataset.filaEtapa; filtrarFila(); }));
    filtrarFila();
})();
