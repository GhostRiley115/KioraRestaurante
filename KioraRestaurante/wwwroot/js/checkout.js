(() => {
    "use strict";
    const formularioCheckout = document.getElementById("formCheckout");
    if (!formularioCheckout) return;
    const confirmarPedido = document.getElementById("confirmarPedido");
    const novo = document.getElementById("endereco-novo");
    const campos = document.getElementById("camposNovoEndereco");
    const salvar = document.getElementById("Dados_NovoEndereco_SalvarNaConta");
    const apelido = document.getElementById("Dados_NovoEndereco_Apelido");
    const grupoApelido = document.getElementById("grupoApelidoEndereco");
    const token = document.getElementById("Dados_TokenEntrega");
    const mensagem = document.getElementById("mensagemEntrega");
    const calcular = document.getElementById("calcularEntrega");
    const moeda = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
    let consultaAtual = 0;

    function invalidarEntrega() {
        consultaAtual++;
        token.value = "";
        formularioCheckout.dataset.validadeEntrega = "";
        confirmarPedido.disabled = true;
        document.getElementById("taxaEntrega").textContent = "A calcular";
        document.getElementById("distanciaEntrega").textContent = "A calcular";
        document.getElementById("totalComEntrega").textContent = "A calcular";
        mensagem.textContent = "Calcule a entrega para conferir a distância e a taxa.";
    }

    function mostrarCampos() {
        campos.hidden = !novo.checked;
        campos.disabled = !novo.checked;
        grupoApelido.hidden = !salvar.checked;
        apelido.disabled = !novo.checked || !salvar.checked;
        apelido.required = novo.checked && salvar.checked;
        if (novo.checked) document.getElementById("Dados_EnderecoVersao").value = "";
    }

    // Guarda a versão do endereço que o cliente escolheu nesta revisão.
    formularioCheckout.querySelectorAll('input[name="Dados.EnderecoId"]').forEach(opcao => {
        opcao.addEventListener("change", () => {
            document.getElementById("Dados_EnderecoVersao").value = opcao.dataset.enderecoVersao ?? "";
            mostrarCampos();
            invalidarEntrega();
        });
    });
    salvar.addEventListener("change", mostrarCampos);
    formularioCheckout.querySelectorAll("[data-campo-entrega]").forEach(campo => {
        campo.addEventListener("input", invalidarEntrega);
        campo.addEventListener("change", invalidarEntrega);
    });
    document.addEventListener("kiora:endereco-excluido", () => {
        if (!formularioCheckout.querySelector('input[name="Dados.EnderecoId"]:checked')) {
            novo.checked = true;
            mostrarCampos();
            invalidarEntrega();
            novo.focus();
        }
    });

    calcular.addEventListener("click", async event => {
        event.preventDefault();
        const consulta = ++consultaAtual;
        token.value = "";
        confirmarPedido.disabled = true;
        calcular.disabled = true;
        calcular.textContent = "Calculando…";
        mensagem.textContent = "Conferindo o endereço e o percurso de entrega…";
        try {
            const resposta = await fetch(formularioCheckout.dataset.cotacaoUrl, {
                method: "POST", body: new FormData(formularioCheckout),
                headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            const tipo = resposta.headers.get("content-type") ?? "";
            if (resposta.redirected || !tipo.includes("application/json"))
                throw new Error("Entre novamente na sua conta para calcular a entrega.");
            const dados = await resposta.json();
            if (consulta !== consultaAtual) return;
            if (!resposta.ok) throw new Error(dados.mensagem ?? "Não foi possível calcular a entrega.");
            const taxa = Number(dados.taxa);
            const distancia = Number(dados.distanciaMetros);
            if (!Number.isFinite(taxa) || taxa < 0 || !Number.isFinite(distancia) || distancia < 0 || distancia > 20000 || !dados.token)
                throw new Error("Não foi possível conferir a entrega. Tente novamente.");
            token.value = dados.token;
            formularioCheckout.dataset.validadeEntrega = dados.expiraEmUtc;
            document.getElementById("taxaEntrega").textContent = moeda.format(taxa);
            document.getElementById("distanciaEntrega").textContent = (distancia / 1000).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " km";
            document.getElementById("totalComEntrega").textContent = moeda.format(Number(formularioCheckout.dataset.totalProdutos) + taxa);
            confirmarPedido.disabled = formularioCheckout.dataset.podeProsseguir !== "true";
            mensagem.textContent = "Entrega disponível. Confira a taxa e o total antes de confirmar.";
        } catch (erro) {
            if (consulta === consultaAtual) {
                invalidarEntrega();
                mensagem.textContent = erro.message;
            }
        } finally {
            calcular.disabled = false;
            calcular.textContent = "Calcular entrega";
        }
    });

    formularioCheckout.addEventListener("submit", function (event) {
        if (event.submitter?.value === "calcular") return;
        // A validação jQuery pode impedir o envio.
        if (window.jQuery && !window.jQuery(formularioCheckout).valid()) return;
        if (!token.value) {
            event.preventDefault();
            mensagem.textContent = "Calcule a entrega antes de confirmar.";
            calcular.focus();
            return;
        }
        confirmarPedido.disabled = true;
        confirmarPedido.textContent = "Confirmando…";
    });

    // Recupera o botão ao voltar pelo histórico do navegador.
    window.addEventListener("pageshow", function () {
        const validade = Date.parse(formularioCheckout.dataset.validadeEntrega);
        if (!token.value || !Number.isFinite(validade) || validade <= Date.now()) {
            invalidarEntrega();
        } else {
            confirmarPedido.disabled = formularioCheckout.dataset.podeProsseguir !== "true";
        }
        confirmarPedido.textContent = "Confirmar pedido";
    });
    mostrarCampos();
})();
