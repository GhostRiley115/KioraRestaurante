// Reserva e fila usam as mesmas regras do servidor. O JavaScript apenas melhora a interação.
(() => {
    const form = document.getElementById("formReserva");
    if (form) {
        const data = form.querySelector("#Dados_Data");
        const pessoas = form.querySelector("#Dados_Pessoas");
        const horario = form.querySelector("#Dados_Horario");
        const aviso = document.getElementById("avisoHorarios");
        let consulta;
        async function atualizar() {
            consulta?.abort(); consulta = new AbortController();
            horario.replaceChildren(new Option("Consultando horários…", ""));
            horario.disabled = true;
            try {
                const url = new URL(form.dataset.horariosUrl, location.origin);
                url.searchParams.set("data", data.value); url.searchParams.set("pessoas", pessoas.value);
                const resposta = await fetch(url, {signal: consulta.signal, credentials: "same-origin"});
                const corpo = await resposta.json();
                if (!resposta.ok) throw new Error(corpo.mensagem || "Não foi possível consultar os horários.");
                horario.replaceChildren(new Option("Selecione um horário", ""));
                for (const h of corpo) {
                    const opcao = new Option(`${h.horario} — até ${h.encerramento} (${h.minutos} min)${h.disponivel ? "" : " · lotado"}`, h.horario);
                    opcao.disabled = !h.disponivel; horario.add(opcao);
                }
                horario.disabled = false;
                aviso.textContent = corpo.some(h => h.disponivel) ? "Disponibilidade sujeita à confirmação." : "Sem horários disponíveis. Escolha outro dia ou entre na fila de hoje.";
            } catch (erro) {
                if (erro.name === "AbortError") return;
                horario.replaceChildren(new Option("Não foi possível consultar", ""));
                aviso.textContent = erro.message;
            }
        }
        data.addEventListener("change", atualizar); pessoas.addEventListener("change", atualizar);
        form.addEventListener("submit", e => {
            if (e.submitter?.value === "reserva" && (!horario.value || horario.disabled)) {
                e.preventDefault(); aviso.textContent = "Escolha um horário disponível antes de confirmar.";
            }
        });
    }
    const painel = document.getElementById("acompanharReserva");
    if (!painel) return;
    const cancelar = document.getElementById("cancelarReserva");
    cancelar.addEventListener("submit", e => { if (!confirm("Cancelar esta solicitação e liberar o espaço?")) e.preventDefault(); });
    const notificar = document.getElementById("notificarReserva");
    notificar.hidden = !("Notification" in window);
    notificar.addEventListener("click", async () => {
        try { const resposta = await Notification.requestPermission();
            notificar.textContent = resposta === "granted" ? "Avisos permitidos com a página aberta" : "Acompanhe os avisos nesta página";
        } catch { notificar.textContent = "Acompanhe os avisos nesta página"; }
    });
    const formatar = valor => new Intl.DateTimeFormat("pt-BR", {timeZone:"America/Sao_Paulo",dateStyle:"short",timeStyle:"short"}).format(new Date(valor.endsWith("Z") ? valor : valor+"Z"));
    let consultando = false;
    async function consultar() {
        if (consultando || document.hidden) return; consultando = true;
        try {
            const resposta = await fetch(painel.dataset.consultaUrl, {credentials:"same-origin",cache:"no-store"});
            if (!resposta.ok) throw new Error("Não foi possível atualizar. Confira sua sessão e recarregue a página.");
            const r = await resposta.json();
            document.getElementById("situacaoReserva").textContent = r.situacaoTexto;
            document.getElementById("posicaoFila").textContent = r.posicaoFila ? `Posição geral na fila: ${r.posicaoFila}. A chamada depende do tamanho do grupo e da mesa.` : "";
            document.getElementById("horarioReserva").textContent = r.inicioUtc ? `Chegada: ${formatar(r.inicioUtc)} · Encerramento: ${formatar(r.fimUtc)}` : "Fila presencial de hoje";
            document.getElementById("mesaReserva").textContent = r.mesa ? `Sua mesa: ${r.mesa}` : "";
            document.getElementById("prazoReserva").textContent = `Prazo para chegada/chamada: ${formatar(r.expiraEmUtc)}.`;
            document.getElementById("avisoChamada").hidden = r.situacao !== 2;
            cancelar.hidden = !r.podeCancelar; cancelar.querySelector('[name="Versao"]').value = r.versao;
            if (r.situacao === 2 && painel.dataset.situacao !== "2" && "Notification" in window && Notification.permission === "granted") {
                try { new Notification("Kiora: é a sua vez!", {body:"Apresente-se à recepção. Confira o prazo na página.",tag:"kiora-reserva"}); } catch { /* O aviso na página continua funcionando. */ }
            }
            painel.dataset.situacao = String(r.situacao);
            document.getElementById("estadoConsulta").textContent = "Situação atualizada. Mantenha esta página aberta para acompanhar.";
        } catch (erro) { document.getElementById("estadoConsulta").textContent = erro.message; }
        finally { consultando = false; }
    }
    setInterval(consultar, 15000);
    document.addEventListener("visibilitychange", consultar);
    consultar();
})();
