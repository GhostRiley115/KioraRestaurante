// Preenche os dados do endereço usando o endpoint do próprio sistema.
(() => {
    "use strict";

    const formulario = document.getElementById("formEndereco");
    if (!formulario) return;

    const cep = document.getElementById("Cep");
    const rua = document.getElementById("Logradouro");
    const bairro = document.getElementById("Bairro");
    const numero = document.getElementById("Numero");
    const mensagem = document.getElementById("mensagemCep");
    const botao = document.getElementById("buscarCep");

    let requisicaoAtual = 0;

    cep.addEventListener("input", () => {
        // Invalida respostas de consultas anteriores.
        requisicaoAtual++;
        mensagem.textContent = "";

        // Evita deixar a rua antiga associada a um CEP novo.
        rua.value = "";
        bairro.value = "";
    });

    botao.addEventListener("click", async () => {
        const valor = cep.value.trim();

        if (!/^[0-9]{5}-?[0-9]{3}$/.test(valor)) {
            mensagem.textContent = "Informe um CEP com oito números.";
            return;
        }

        const consulta = ++requisicaoAtual;
        botao.disabled = true;
        mensagem.textContent = "Consultando CEP…";

        try {
            const resposta = await fetch(
                formulario.dataset.cepBase + encodeURIComponent(valor),
                { headers: { "Accept": "application/json" } }
            );

            // Login expirado pode produzir um redirecionamento para uma página.
            const tipo = resposta.headers.get("content-type") ?? "";

            if (resposta.redirected || !tipo.includes("application/json")) {
                throw new Error(
                    "Não foi possível consultar. Confira se você continua conectado."
                );
            }

            const dados = await resposta.json();

            // O usuário pode ter alterado o CEP enquanto a consulta estava em andamento.
            if (consulta !== requisicaoAtual) return;

            if (!resposta.ok) {
                throw new Error(dados.mensagem ?? "Não foi possível consultar o CEP.");
            }

            cep.value = dados.cep;
            rua.value = dados.logradouro;
            bairro.value = dados.bairro;

            mensagem.textContent =
                `${dados.cidade}/${dados.uf}. Confira os dados e complete o número.`;

            if (!dados.logradouro || !dados.bairro) {
                mensagem.textContent +=
                    " Esse CEP não informa todos os campos; preencha os que faltam.";
            }

            numero.focus();
        } catch (erro) {
            if (consulta === requisicaoAtual) {
                mensagem.textContent = erro.message;
            }
        } finally {
            botao.disabled = false;
        }
    });
})();