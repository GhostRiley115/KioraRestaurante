(() => {
    "use strict";
    const sino = document.getElementById("sinoNotificacoes");
    if (!sino) return;
    const painel = document.getElementById("painelNotificacoes");
    const lista = painel?.querySelector("[data-lista-notificacoes]");
    const estado = painel?.querySelector("[data-estado-notificacoes]");
    const modelo = document.getElementById("modeloTituloNotificacao");
    let carregandoPainel = false;
    function contar(quantidade) {
        if (!Number.isInteger(quantidade) || quantidade < 0) return;
        contador.textContent = quantidade > 99 ? "99+" : String(quantidade);
        contador.hidden = quantidade === 0;
        sino.setAttribute("aria-label", quantidade > 0 ? `Notificações: ${quantidade} não lida${quantidade === 1 ? "" : "s"}` : "Notificações");
    }
    async function abrirPainel() {
        if (!painel || !lista || !estado || !modelo || carregandoPainel || sessaoEncerrada) return;
        carregandoPainel = true;
        painel.setAttribute("aria-busy", "true");
        estado.textContent = "Carregando notificações…";
        try {
            const resposta = await fetch(sino.dataset.painelUrl, { headers: { Accept: "application/json" }, cache: "no-store" });
            if (resposta.redirected || [401, 403].includes(resposta.status)) {
                sessaoEncerrada = true; throw new Error("Entre novamente na sua conta para consultar os avisos.");
            }
            if (!resposta.ok || !(resposta.headers.get("content-type") ?? "").includes("application/json"))
                throw new Error("Não foi possível consultar os avisos. Feche e abra o sino para tentar novamente.");
            const dados = await resposta.json();
            if (!Array.isArray(dados.itens) || dados.itens.some(n => typeof n.id !== "string" || typeof n.titulo !== "string" || typeof n.naoLida !== "boolean"))
                throw new Error("Não foi possível conferir os avisos. Tente novamente.");
            const fragmento = document.createDocumentFragment();
            for (const aviso of dados.itens) {
                const item = modelo.content.cloneNode(true);
                const botao = item.querySelector("button");
                botao.value = aviso.id;
                botao.classList.toggle("notificacao-nao-lida", aviso.naoLida);
                item.querySelector("[data-titulo-notificacao]").textContent = aviso.titulo;
                item.querySelector("[data-aviso-nao-lido]").hidden = !aviso.naoLida;
                fragmento.append(item);
            }
            lista.replaceChildren(fragmento);
            estado.textContent = dados.itens.length ? "" : "Você ainda não tem notificações.";
            contar(dados.naoLidas);
        } catch (erro) { estado.textContent = erro.message; }
        finally { carregandoPainel = false; painel.removeAttribute("aria-busy"); }
    }
    sino.addEventListener("shown.bs.dropdown", abrirPainel);
    const contador = sino.querySelector("[data-contador-notificacoes]");
    let consultando = false;
    let sessaoEncerrada = false;
    async function atualizar() {
        if (document.hidden || consultando || sessaoEncerrada) return;
        consultando = true;
        try {
            const resposta = await fetch(sino.dataset.resumoUrl, { headers: { Accept: "application/json" }, cache: "no-store" });
            if (resposta.redirected || [401, 403].includes(resposta.status)) { sessaoEncerrada = true; return; }
            if (!resposta.ok || !(resposta.headers.get("content-type") ?? "").includes("application/json")) return;
            const dados = await resposta.json();
            if (!Number.isInteger(dados.naoLidas) || dados.naoLidas < 0) return;
            contar(dados.naoLidas);
        } catch { /* Uma falha temporária não interrompe a navegação; a lista continua acessível. */ }
        finally { consultando = false; }
    }
    atualizar();
    setInterval(atualizar, 30000);
    document.addEventListener("visibilitychange", atualizar);
    window.addEventListener("pageshow", atualizar);
})();
