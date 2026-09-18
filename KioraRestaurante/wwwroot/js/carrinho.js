(() => {
    "use strict";

    //Pega os elementos principais do HTML que o JavaScript vai controlar.
    const configuracao = document.getElementById("carrinhoConfiguracao");
    const elementoPainel = document.getElementById("painelCarrinho");

    //Se os elementos principais não existirem, encerra o script.// Se os elementos principais não existirem, encerra o script.
    if (!configuracao || !elementoPainel) return;

    const apiBase = configuracao.dataset.api;
    const raiz = configuracao.dataset.raiz;

    //Pega o token antiforgery gerado pelo ASP.NET.
    const token = configuracao.querySelector(
        'input[name="__RequestVerificationToken"]'
    ).value;

    //Cria/recupera o componente Offcanvas do Bootstrap.
    const painel = bootstrap.Offcanvas.getOrCreateInstance(elementoPainel);

    //Elementos usados para exibir e atualizar o carrinho.
    const lista = document.getElementById("itensCarrinho");
    const modelo = document.getElementById("modeloItemCarrinho");
    const vazio = document.getElementById("carrinhoVazio");
    const total = document.getElementById("totalCarrinho");
    const contador = document.getElementById("quantidadeCarrinho");
    const abrir = document.getElementById("abrirCarrinho");
    const avisos = document.getElementById("avisosCarrinho");
    const estado = document.getElementById("estadoCarrinho");
    const notificacao = document.getElementById("avisoCarrinho");

    //Botões e formulário do carrinho.
    const botaoRevisar = document.getElementById("revisarCarrinho");
    const botaoEsvaziar = document.getElementById("esvaziarCarrinho");
    const botaoAtualizar = document.getElementById("atualizarCarrinho");

    const formMesclagem = document.getElementById("formMesclagem");
    const listaConflitos = document.getElementById("conflitosCarrinho");

    //Formata valores numéricos como moeda brasileira.
    const moeda = new Intl.NumberFormat("pt-BR", {
        style: "currency",
        currency: "BRL"
    });

    //Guarda o carrinho atual recebido da API.
    let carrinho = null;
    //Impede várias operações no carrinho ao mesmo tempo.
    let ocupado = false;
    //Indica se os dados atuais do carrinho foram confirmados pela API.
    let dadosConfiaveis = false;
    //Guarda o temporizador usado para esconder notificações.
    let temporizadorMensagem;

    //Mostra uma mensagem temporária para o usuário.
    function avisar(texto, erro = false) {
        //Cancela a mensagem anterior, se ainda estiver ativa.
        clearTimeout(temporizadorMensagem);

        notificacao.textContent = texto;
        notificacao.dataset.erro = String(erro);
        notificacao.hidden = false;

        //Esconde a mensagem depois de alguns segundos.
        temporizadorMensagem = setTimeout(() => {
            notificacao.hidden = true;
        }, erro ? 10000 : 4500);
    }


    // CONFIRMAÇÃO VISUAL: só é chamada depois da resposta de sucesso da API.
    // A mensagem de texto continua disponível mesmo sem animações.
    function animarAdicao(formulario, quantidade) {
        if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
        const origem = formulario.querySelector('button[type="submit"]');
        if (!origem || !abrir || typeof origem.animate !== "function") return;
        const inicio = origem.getBoundingClientRect();
        const destino = abrir.getBoundingClientRect();
        // No menu recolhido do celular, confirma no botão sem voar para um alvo oculto.
        origem.animate([{ opacity: 0.55 }, { opacity: 1 }], { duration: 450 });
        if (!destino.width || !destino.height || destino.bottom < 0 || destino.top > innerHeight) return;
        const indicador = document.createElement("span");
        indicador.className = "kc-adicao-voando";
        indicador.textContent = `+${quantidade}`;
        indicador.setAttribute("aria-hidden", "true");
        indicador.style.left = `${inicio.left + inicio.width / 2 - 22}px`;
        indicador.style.top = `${inicio.top + inicio.height / 2 - 22}px`;
        document.body.append(indicador);
        const x = destino.left + destino.width / 2 - (inicio.left + inicio.width / 2);
        const y = destino.top + destino.height / 2 - (inicio.top + inicio.height / 2);
        const voo = indicador.animate([
            { transform: "translate(0, 0) scale(1)", opacity: 1 },
            { transform: `translate(${x}px, ${y}px) scale(0.45)`, opacity: 0.3 }
        ], { duration: 650, easing: "ease-in-out" });
        // Remove o elemento temporário tanto ao concluir quanto ao cancelar.
        voo.finished.then(() => {
            indicador.remove();
            abrir.animate([{ transform: "scale(1)" }, { transform: "scale(1.2)" },
                { transform: "scale(1)" }], { duration: 300 });
        }, () => indicador.remove());
    }

    //Centraliza as chamadas para a API do carrinho.

    //Recebe:
    //- caminho da API;
    //- metodo http;
    //- corpo da requisição;
    //- se deve aceitar conflitos de mesclagem.

    async function chamarApi(
        caminho = "",
        metodo = "GET",
        corpo = undefined,
        aceitarConflitos = false
    ) {
        //Cabeçalhos enviados para a API.
        const headers = {
            "Accept": "application/json"
        };

        //Operações diferentes de GET precisam enviar o token antiforgery.
        if (metodo !== "GET") {
            headers["X-CSRF-TOKEN"] = token;
        }

        //Se houver dados para enviar, informa que o corpo é JSON.
        if (corpo !== undefined) {
            headers["Content-Type"] = "application/json";
        }

        //Faz a requisição para a API.
        const resposta = await fetch(apiBase + caminho, {
            method: metodo,
            headers,
            credentials: "same-origin",
            cache: "no-store",
            //Só envia um body quando existe conteúdo para enviar.
            body: corpo === undefined
                ? undefined
                : JSON.stringify(corpo)
        });

        //Tenta transformar a resposta da API em objeto JavaScript.
        const dados = await resposta.json().catch(() => null);

        //Se a requisição foi redirecionada, a sessão pode ter mudado.
        if (resposta.redirected) {
            throw new Error("Sua sessão mudou. Recarregue a página.");
        }

        /*
        409 também pode ser usado pela mesclagem para informar
        que existem conflitos que precisam ser resolvidos.
         */
        const conflitoDeMesclagem =
            aceitarConflitos
            && resposta.status === 409
            && dados?.concluida === false
            && Array.isArray(dados.conflitos);

        //Nesse caso, devolve os conflitos sem tratar como erro comum.
        if (conflitoDeMesclagem) return dados;

        //Qualquer outra resposta HTTP com erro entra aqui.
        if (!resposta.ok) {
            //Junta possíveis mensagens de validação enviadas pelo ASP.NET.
            const errosValidacao = dados?.errors
                ? Object.values(dados.errors).flat().join(" ")
                : "";

            //Tenta encontrar a melhor mensagem de erro.
            let mensagem =
                dados?.mensagem
                || dados?.message
                || errosValidacao;

            //Se a API não enviou uma mensagem, cria uma padrão.
            if (!mensagem) {
                mensagem = resposta.status === 401
                    ? "Entre novamente na sua conta."
                    : resposta.status === 400
                        ? "Verifique os dados. Se necessário, recarregue a página."
                        : "Não foi possível concluir a operação.";
            }

            throw new Error(mensagem);
        }

        //A API deveria ter devolvido dados.
        if (!dados) {
            throw new Error("A resposta recebida não pôde ser interpretada.");
        }

        return dados;
    }

    /*
    Atualiza o estado dos botões e controles do carrinho.
    Os controles são bloqueados enquanto:
    - existe uma operação em andamento;
    - os dados ainda não foram confirmados;
    - existe uma mesclagem pendente.
    */
    function atualizarControles() {
        const bloquear =
            ocupado
            || !dadosConfiaveis
            || Boolean(carrinho?.mesclagemPendente);

        //Atualiza todos os botões que alteram o carrinho.
        document.querySelectorAll("[data-carrinho-mutacao]")
            .forEach(elemento => {
                elemento.disabled =
                    bloquear || elemento.dataset.bloqueado === "true";
            });

        //Só permite revisar se o backend disser que pode prosseguir.
        botaoRevisar.disabled =
            bloquear || !carrinho?.podeProsseguir;

        //Só permite esvaziar se existirem itens.
        botaoEsvaziar.disabled =
            bloquear || !carrinho?.itens?.length;

        //Não permite outra atualização enquanto uma já estiver acontecendo.
        botaoAtualizar.disabled = ocupado;

        //Bloqueia os campos da mesclagem durante uma operação.
        formMesclagem.querySelectorAll("input, button")
            .forEach(elemento => {
                elemento.disabled = ocupado || !dadosConfiaveis;
            });

        //Informa aos leitores de tela que o painel está ocupado.
        elementoPainel.setAttribute("aria-busy", String(ocupado));

        //Atualiza a mensagem de estado do carrinho.
        estado.textContent = ocupado
            ? "Atualizando…"
            : dadosConfiaveis
                ? "Carrinho atualizado"
                : "Atualize o carrinho para continuar.";
    }

    /*
    Executa uma operação do carrinho com proteção contra
    cliques/requisições simultâneas e tratamento de erros.
    */
    async function executar(operacao) {
        //Impede iniciar outra operação enquanto uma estiver acontecendo.
        if (ocupado) return;

        ocupado = true;
        atualizarControles();

        try {
            //Executa a função recebida.
            await operacao();
        } catch (erro) {
            //Depois de um erro, os dados precisam ser atualizados novamente.
            dadosConfiaveis = false;

            avisar(
                `${erro.message} Use “Atualizar” no carrinho antes de tentar novamente.`,
                true
            );
        } finally {
            //Libera os controles novamente.
            ocupado = false;
            atualizarControles();
        }
    }

    /*
    Prepara a imagem de um produto.
    Se a URL for válida, mostra a imagem.
    Se houver erro, mantém o ícone de imagem.
     */
    function prepararImagem(elemento, caminho) {
        //Sem URL, não há imagem para carregar.
        if (!caminho) return;

        //Converte caminhos iniciados por "~/" para a raiz do site.
        const normalizado = caminho.startsWith("~/")
            ? raiz + caminho.slice(2)
            : caminho;

        try {
            const url = new URL(normalizado, document.baseURI);

            // Aceita somente URLs HTTP ou HTTPS.
            if (!["http:", "https:"].includes(url.protocol)) return;

            // Se a imagem falhar, esconde a imagem e mantém o ícone.
            elemento.addEventListener("error", () => {
                elemento.hidden = true;
            }, { once: true });

            elemento.src = url.href;
            elemento.hidden = false;
        } catch {
            // URL inválida: mantém o ícone de imagem.
        }
    }

    /*
    Atualiza toda a interface com os dados mais recentes do carrinho.
    Recebe o CarrinhoResponseDTO enviado pela API.
    */
    function renderizar(novoCarrinho) {
        //Garante que a resposta realmente possui uma lista de itens.
        if (!Array.isArray(novoCarrinho?.itens)) {
            throw new Error("A resposta não contém os itens do carrinho.");
        }

        // Guarda os novos dados do carrinho.
        carrinho = novoCarrinho;
        dadosConfiaveis = true;

        // Remove os itens e avisos antigos antes de recriar a interface.
        lista.replaceChildren();
        avisos.replaceChildren();

        // Cria um item visual para cada produto do carrinho.
        for (const item of carrinho.itens) {
            // Copia o <template> do HTML.
            const copia = modelo.content.cloneNode(true);
            const linha = copia.querySelector(".kc-item");

            // Guarda o ID do produto no HTML.
            linha.dataset.produtoId = item.produtoId;

            // Preenche o nome do produto.
            copia.querySelector(".kc-item-nome").textContent =
                item.nomeProduto;

            // Mostra o preço por unidade.
            copia.querySelector(".kc-item-preco").textContent =
                `${moeda.format(item.precoUnitario)} por unidade`;

            // Mostra a quantidade atual.
            copia.querySelector(".kc-item-quantidade").textContent =
                item.quantidade;

            // Mostra o subtotal daquele produto.
            copia.querySelector(".kc-item-subtotal").textContent =
                moeda.format(item.subtotal);

            // Mostra ou esconde o aviso daquele produto.
            const avisoItem = copia.querySelector(".kc-item-aviso");
            avisoItem.textContent = item.aviso || "";
            avisoItem.hidden = !item.aviso;

            // Identifica o grupo de controle de quantidade.
            const grupo = copia.querySelector(".kc-quantidade");
            grupo.setAttribute(
                "aria-label",
                `Quantidade de ${item.nomeProduto}`
            );

            // Pega os três botões do item.
            const diminuir = copia.querySelector('[data-acao="diminuir"]');
            const aumentar = copia.querySelector('[data-acao="aumentar"]');
            const remover = copia.querySelector('[data-acao="remover"]');

            // Define o texto que leitores de tela devem anunciar.
            diminuir.setAttribute(
                "aria-label",
                item.quantidade === 1
                    ? `Remover ${item.nomeProduto}`
                    : `Diminuir quantidade de ${item.nomeProduto}`
            );

            aumentar.setAttribute(
                "aria-label",
                `Aumentar quantidade de ${item.nomeProduto}`
            );

            remover.setAttribute(
                "aria-label",
                `Remover ${item.nomeProduto}`
            );

            // Se o produto estiver indisponível, bloqueia diminuir.
            diminuir.dataset.bloqueado =
                String(!item.disponivelParaCompra);

            // Bloqueia aumentar se o produto não puder ser comprado
            // ou se já estiver no limite de 30 unidades.
            aumentar.dataset.bloqueado = String(
                !item.disponivelParaCompra || item.quantidade >= 30
            );

            // Tenta carregar a imagem do produto.
            prepararImagem(
                copia.querySelector(".kc-imagem"),
                item.imagemUrl
            );

            // Coloca o item pronto dentro da lista do carrinho.
            lista.append(copia);
        }

        // Adiciona os avisos gerais retornados pela API.
        for (const mensagem of carrinho.avisos || []) {
            const li = document.createElement("li");
            li.textContent = mensagem;
            avisos.append(li);
        }

        // Mostra os avisos somente quando houver algum.
        avisos.hidden = !avisos.children.length;
        // Mostra a área de carrinho vazio quando não houver itens.
        vazio.hidden = carrinho.itens.length > 0;

        // Atualiza o valor total.
        total.textContent = moeda.format(carrinho.total);

        // Atualiza o contador de itens do ícone do carrinho.
        contador.textContent = carrinho.quantidadeTotal;
        contador.hidden = carrinho.quantidadeTotal === 0;

        // Atualiza o texto de acessibilidade do botão do carrinho.
        abrir.setAttribute(
            "aria-label",
            `Abrir carrinho: ${carrinho.quantidadeTotal} unidades`
        );

        // Recalcula quais controles devem ficar habilitados.
        atualizarControles();
    }

    //Mostra na tela os conflitos encontrados durante a mesclagem.
    function mostrarConflitos(conflitos) {
        // Remove conflitos antigos.
        listaConflitos.replaceChildren();

        // Cria um campo para cada conflito.
        for (const conflito of conflitos) {
            const grupo = document.createElement("div");
            grupo.className = "kc-conflito";

            const label = document.createElement("label");
            const input = document.createElement("input");
            const detalhe = document.createElement("small");

            // Configura o campo de quantidade.
            input.id = `mesclar-${conflito.produtoId}`;
            input.type = "number";
            input.min = "1";
            input.max = "30";
            input.step = "1";
            input.required = true;

            // Guarda o ID do produto no próprio input.
            input.dataset.produtoId = conflito.produtoId;

            // Obriga o cliente a escolher uma quantidade.
            input.value = "";

            // Liga o label ao input.
            label.htmlFor = input.id;
            label.textContent = conflito.nomeProduto;

            // Explica o motivo do conflito.
            detalhe.textContent =
                `${conflito.quantidadeVisitante} no carrinho visitante + `
                + `${conflito.quantidadeUsuario} na conta = `
                + `${conflito.quantidadeSomada}. Escolha de 1 a 30.`;

            // Junta os elementos e coloca na tela.
            grupo.append(label, detalhe, input);
            listaConflitos.append(grupo);
        }

        // Mostra o formulário de mesclagem.
        formMesclagem.hidden = false;
        // Abre o painel do carrinho.
        painel.show();
    }

    //Envia as escolhas dos conflitos para a API e trata o resultado da mesclagem.
    async function mesclar(quantidadesResolvidas = {}) {
        const resultado = await chamarApi(
            "/mesclagem",
            "POST",
            { quantidadesResolvidas },
            true
        );

        // Se ainda existem conflitos, mostra-os para o cliente resolver.
        if (!resultado.concluida) {
            mostrarConflitos(resultado.conflitos);
            return;
        }

        // Mesclagem concluída: limpa a área de conflitos.
        formMesclagem.hidden = true;
        listaConflitos.replaceChildren();

        // Mostra o carrinho final.
        renderizar(resultado.carrinho);
    }

    //Busca o carrinho novamente na API e atualiza a interface.
    async function sincronizar() {
        // GET /api/carrinho
        const resposta = await chamarApi();

        // Atualiza a tela com os dados recebidos. renderizar(resposta);
        renderizar(resposta);

        // Se houver um carrinho visitante pendente,
        // tenta iniciar a mesclagem.
        if (resposta.mesclagemPendente) {
            await mesclar();
        } else {
            formMesclagem.hidden = true;
            listaConflitos.replaceChildren();
        }
    }

    //Quando o formulário de mesclagem for enviado, coleta as quantidades escolhidas pelo cliente.
    formMesclagem.addEventListener("submit", evento => {
        // Impede o navegador de recarregar a página.
        evento.preventDefault();

        // Verifica se todos os campos obrigatórios foram preenchidos.
        if (!formMesclagem.reportValidity()) return;

        const ajustes = {};

        // Percorre todos os campos de quantidade.
        listaConflitos.querySelectorAll("input").forEach(input => {
            // Usa o ID do produto como chave
            // e a quantidade escolhida como valor.
            ajustes[input.dataset.produtoId] = Number(input.value);
        });

        // Envia as escolhas para a API.
        executar(async () => {
            await mesclar(ajustes);

            // Se a mesclagem terminou, avisa o usuário.
            if (!carrinho.mesclagemPendente) {
                avisar("Seus carrinhos foram reunidos.");
            }
        });
    });

    // Atualiza o carrinho quando o botão "Atualizar" for clicado.
    botaoAtualizar.addEventListener("click", () => {
        executar(sincronizar);
    });

    // Ao abrir o painel, busca os dados atuais do carrinho.
    elementoPainel.addEventListener("shown.bs.offcanvas", () => {
        executar(sincronizar);
    });

    //Atualiza os botões + e - da quantidade no cardápio.
    function atualizarSeletor(form) {
        const input = form.elements.quantidade;
        const quantidade = Number(input.value);

        // Bloqueia diminuir quando chegar em 1.
        form.querySelector('[data-seletor="-1"]').dataset.bloqueado =
            String(quantidade <= 1);

        // Bloqueia aumentar quando chegar em 30.
        form.querySelector('[data-seletor="1"]').dataset.bloqueado =
            String(quantidade >= 30);

        atualizarControles();
    }

    //Configura os controles de quantidade dos produtos exibidos no cardápio.
    document.querySelectorAll(".kc-compra").forEach(form => {
        const input = form.elements.quantidade;

        // Define o estado inicial dos botões + e -.
        atualizarSeletor(form);

        // Configura cada botão de quantidade.
        form.querySelectorAll("[data-seletor]").forEach(botao => {
            botao.addEventListener("click", () => {
                const atual = Number(input.value);

                // Se o valor atual não for um número inteiro,
                // começa novamente em 1.
                const base = Number.isInteger(atual) ? atual : 1;
                // O botão pode representar +1 ou -1.
                const passo = Number(botao.dataset.seletor);

                // Mantém a quantidade sempre entre 1 e 30.
                input.value = Math.min(30, Math.max(1, base + passo));

                atualizarSeletor(form);
            });
        });

        // Atualiza os botões enquanto o usuário digita.
        input.addEventListener("input", () => {
            atualizarSeletor(form);
        });

        //Envia o produto para o carrinho quando o formulário do cardápio for enviado.
        form.addEventListener("submit", evento => {
            // Impede o envio tradicional do formulário.
            evento.preventDefault();

            // Verifica se a quantidade é válida.
            if (!form.reportValidity()) return;

            // Se os dados atuais não forem confiáveis
            // ou houver mesclagem pendente, abre o carrinho.
            if (!dadosConfiaveis || carrinho?.mesclagemPendente) {
                painel.show();
                return;
            }

            // Pega o produto e a quantidade escolhida.
            const produtoId = Number(form.dataset.produtoId);
            const quantidade = Number(input.value);

            // Envia o produto para a API.
            executar(async () => {
                const resposta = await chamarApi(
                    "/itens",
                    "POST",
                    { produtoId, quantidade }
                );

                // Atualiza o carrinho com a resposta da API.
                renderizar(resposta);

                // Só volta para 1 depois que a API confirmar.
                input.value = "1";
                atualizarSeletor(form);

                avisar(
                    `${quantidade} unidade(s) adicionada(s) ao carrinho.`
                );
                animarAdicao(form, quantidade);
            });
        });
    });

    /*
    Escuta cliques nos botões +, -, e Remover dentro da lista do carrinho.
    Como os itens são criados dinamicamente, usamos o elemento "lista" para capturar os cliques.
    */
    lista.addEventListener("click", async evento => {
        // Descobre se o clique foi em um botão de ação.
        const botao = evento.target.closest("button[data-acao]");

        // Ignora cliques que não sejam válidos.
        if (!botao || botao.disabled || !dadosConfiaveis) return;

        // Encontra a linha do produto clicado.
        const linha = botao.closest("[data-produto-id]");
        const produtoId = Number(linha.dataset.produtoId);
        const acao = botao.dataset.acao;

        // Encontra o item correspondente nos dados do carrinho.
        const item = carrinho.itens.find(
            produto => produto.produtoId === produtoId
        );

        if (!item) return;

        await executar(async () => {
            let resposta;

            //Se clicou em remover ou em diminuir quando a quantidade já é 1, o produto deve ser removido.
            const deveRemover =
                acao === "remover"
                || (acao === "diminuir" && item.quantidade === 1);

            if (deveRemover) {
                // DELETE /api/carrinho/itens/{produtoId}
                resposta = await chamarApi(
                    `/itens/${produtoId}`,
                    "DELETE"
                );
            } else {
                // Aumenta ou diminui uma unidade.
                const quantidade =
                    item.quantidade + (acao === "aumentar" ? 1 : -1);

                // PUT /api/carrinho/itens/{produtoId}
                resposta = await chamarApi(
                    `/itens/${produtoId}`,
                    "PUT",
                    { quantidade }
                );
            }

            // Atualiza a interface com a resposta da API.
            renderizar(resposta);
        });

        //renderizar() recriou os elementos do carrinho. Por isso o botão clicado deixou de existir.
        //Aqui procuramos o novo botão e devolvemos o foco para facilitar o uso pelo teclado.
        const novoBotao = lista.querySelector(
            `[data-produto-id="${produtoId}"] [data-acao="${acao}"]`
        );

        if (novoBotao && !novoBotao.disabled) {
            novoBotao.focus();
        } else {
            lista.focus();
        }
    });

    // Esvazia o carrinho inteiro.
    botaoEsvaziar.addEventListener("click", () => {
        // Não faz nada se não houver itens
        if (!carrinho?.itens.length) return;

        executar(async () => {
            // DELETE /api/carrinho/itens
            const resposta = await chamarApi("/itens", "DELETE");

            renderizar(resposta);
            avisar("Carrinho esvaziado.");
        });
    });

    //Abre o modal de login. Se o painel do carrinho estiver aberto, ele é fechado primeiro.
    function abrirLogin() {
        const elementoLogin = document.getElementById("modalLogin");

        // Se não houver modal, avisa o usuário.
        if (!elementoLogin) {
            avisar("Abra o menu da conta para entrar.", true);
            return;
        }

        // Função responsável por abrir o modal.
        const mostrarLogin = () => {
            bootstrap.Modal.getOrCreateInstance(elementoLogin).show();
        };

        // Se o carrinho estiver aberto, fecha primeiro.
        if (elementoPainel.classList.contains("show")) {
            elementoPainel.addEventListener(
                "hidden.bs.offcanvas",
                mostrarLogin,
                { once: true }
            );

            painel.hide();
        } else {
            // Caso contrário, abre o login imediatamente.
            mostrarLogin();
        }
    }

    //Revisa o carrinho antes de permitir que o usuário avance para o pedido.
    botaoRevisar.addEventListener("click", () => {
        executar(async () => {
            // Busca novamente os preços e a disponibilidade atuais.
            const resposta = await chamarApi("/revisao");

            renderizar(resposta);

            // Se houver algum problema, impede o avanço.
            if (!resposta.podeProsseguir) {
                avisar("Confira os avisos do carrinho para continuar.", true);
                return;
            }

            // Se ainda não estiver logado, abre o login.
            if (resposta.requerLogin) {
                abrirLogin();
                return;
            }

            avisar("Itens revisados. Confira as quantidades e os valores.");

            /*
            Ponto de integração com a futura tela de checkout.
            Nenhum pedido é criado aqui.
            */
            document.dispatchEvent(new CustomEvent(
                "kiora:carrinho-revisado",
                { detail: resposta }
            ));
        });
    });

    document.addEventListener("kiora:carrinho-revisado", () => {
        window.location.assign(configuracao.dataset.checkout);
    });

    // Faz a primeira consulta ao abrir uma página que possui o carrinho.
    executar(sincronizar);

})();