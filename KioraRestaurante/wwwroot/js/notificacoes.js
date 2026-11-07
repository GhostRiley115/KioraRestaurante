(() => {
    "use strict";
    const sino = document.getElementById("sinoNotificacoes");
    if (!sino) return;
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
            contador.textContent = dados.naoLidas > 99 ? "99+" : String(dados.naoLidas);
            contador.hidden = dados.naoLidas === 0;
            sino.setAttribute("aria-label", dados.naoLidas > 0 ? `Notificações: ${dados.naoLidas} não lidas` : "Notificações");
        } catch { /* Uma falha temporária não interrompe a navegação; a lista continua acessível. */ }
        finally { consultando = false; }
    }
    atualizar();
    setInterval(atualizar, 30000);
    document.addEventListener("visibilitychange", atualizar);
    window.addEventListener("pageshow", atualizar);
})();
