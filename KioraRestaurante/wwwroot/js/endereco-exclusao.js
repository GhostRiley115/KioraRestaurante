(() => {
    "use strict";
    const dialogo = document.getElementById("confirmarExclusaoEndereco");
    if (!dialogo) return;
    const executar = document.getElementById("executarExclusaoEndereco");
    const mensagem = document.getElementById("mensagemExclusaoEndereco");
    let selecionado = null;

    document.addEventListener("click", event => {
        const botao = event.target.closest("[data-excluir-endereco]");
        if (!botao) return;
        event.preventDefault();
        selecionado = botao;
        document.getElementById("nomeExcluirEndereco").textContent = botao.dataset.enderecoNome;
        dialogo.showModal();
    });
    document.getElementById("cancelarExclusaoEndereco").addEventListener("click", () => dialogo.close());
    dialogo.addEventListener("close", () => selecionado?.focus());

    executar.addEventListener("click", async () => {
        if (!selecionado) return;
        const botao = selecionado;
        executar.disabled = true;
        try {
            const dados = new FormData();
            dados.set("id", botao.dataset.enderecoId);
            dados.set("versao", botao.dataset.enderecoVersaoExclusao);
            dados.set("__RequestVerificationToken", botao.closest("form").querySelector('input[name="__RequestVerificationToken"]').value);
            const resposta = await fetch(botao.dataset.exclusaoUrl, {
                method: "POST", body: dados,
                headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            const tipo = resposta.headers.get("content-type") ?? "";
            if (resposta.redirected || !tipo.includes("application/json"))
                throw new Error("Entre novamente na sua conta antes de excluir o endereço.");
            const resultado = await resposta.json();
            if (!resposta.ok) throw new Error(resultado.mensagem ?? "Não foi possível excluir o endereço.");
            const cartao = botao.closest("[data-endereco-cartao]");
            cartao.remove();
            selecionado = null;
            dialogo.close();
            mensagem.textContent = resultado.mensagem;
            mensagem.setAttribute("tabindex", "-1");
            mensagem.focus();
            document.dispatchEvent(new CustomEvent("kiora:endereco-excluido"));
            const vazio = document.getElementById("enderecosVazios");
            if (vazio) vazio.hidden = !!document.querySelector("[data-endereco-cartao]");
        } catch (erro) {
            dialogo.close();
            mensagem.textContent = erro.message;
        } finally {
            executar.disabled = false;
        }
    });
})();
