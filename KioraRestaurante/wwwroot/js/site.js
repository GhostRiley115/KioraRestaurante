// ================================================================
// CADASTRO DE USUÁRIO
// ================================================================

// Localiza o formulário de cadastro pelo ID.
const formCadastro = document.getElementById("formCadastro");

// Localiza a área onde serão exibidas as mensagens.
const mensagemCadastro = document.getElementById("mensagemCadastro");


// ================================================================
// VERIFICAÇÃO DOS ELEMENTOS
// ================================================================

// Verifica se o formulário e a área de mensagem existem.
if (formCadastro && mensagemCadastro) {


    // ============================================================
    // ENVIO DO FORMULÁRIO
    // ============================================================

    formCadastro.addEventListener("submit", async function (event) {

        // Impede o envio tradicional do formulário.
        event.preventDefault();


        // ========================================================
        // LIMPAR MENSAGEM ANTERIOR
        // ========================================================

        mensagemCadastro.style.display = "none";
        mensagemCadastro.textContent = "";


        // ========================================================
        // PEGAR DADOS DO FORMULÁRIO
        // ========================================================

        const dados = new FormData(formCadastro);


        try {

            // ====================================================
            // ENVIA OS DADOS PARA O ACCOUNTCONTROLLER
            // ====================================================

            const resposta = await fetch("/Account/Cadastro", {
                method: "POST",
                body: dados
            });


            // ====================================================
            // DEBUG - RESPOSTA DO SERVIDOR
            // ====================================================

            console.log("Status da resposta:", resposta.status);
            console.log("Resposta OK:", resposta.ok);


            // Lê a resposta como texto.
            const textoResposta = await resposta.text();

            console.log("Resposta do servidor:", textoResposta);


            // ====================================================
            // CONVERTER RESPOSTA PARA JSON
            // ====================================================

            let resultado;

            try {

                resultado = JSON.parse(textoResposta);

            }
            catch {

                resultado = {
                    sucesso: false,
                    mensagem: "O servidor retornou uma resposta inesperada."
                };

            }


            // ====================================================
            // CADASTRO REALIZADO COM SUCESSO
            // ====================================================

            if (resposta.ok && resultado.sucesso) {

                mensagemCadastro.textContent = resultado.mensagem;

                mensagemCadastro.className =
                    "mensagem-cadastro mensagem-cadastro-sucesso";

                mensagemCadastro.style.display = "block";


                // Limpa o formulário somente após o cadastro.
                formCadastro.reset();

            }


            // ====================================================
            // ERRO NO CADASTRO
            // ====================================================

            else {

                mensagemCadastro.textContent =
                    resultado.mensagem ||
                    "Não foi possível realizar o cadastro.";

                mensagemCadastro.className =
                    "mensagem-cadastro mensagem-cadastro-erro";

                mensagemCadastro.style.display = "block";

            }

        }
        catch (erro) {

            // ====================================================
            // ERRO DE COMUNICAÇÃO
            // ====================================================

            mensagemCadastro.textContent =
                "Não foi possível realizar o cadastro. Tente novamente.";

            mensagemCadastro.className =
                "mensagem-cadastro mensagem-cadastro-erro";

            mensagemCadastro.style.display = "block";


            console.error("Erro no cadastro:", erro);

        }

    });

}


// ================================================================
// LIMPAR FORMULÁRIO AO FECHAR O MODAL
// ================================================================

// Localiza o modal de cadastro pelo ID.
const modalCadastro = document.getElementById("modalCadastro");


// Verifica se o modal, o formulário e a mensagem existem na página.
if (modalCadastro && formCadastro && mensagemCadastro) {


    // Executa quando o modal terminar de ser fechado.
    modalCadastro.addEventListener("hidden.bs.modal", function () {


        // ========================================================
        // LIMPAR CAMPOS DO FORMULÁRIO
        // ========================================================

        // Limpa todos os campos do formulário.
        formCadastro.reset();


        // ========================================================
        // LIMPAR MENSAGEM
        // ========================================================

        // Remove o texto da mensagem.
        mensagemCadastro.textContent = "";


        // Esconde novamente a área da mensagem.
        mensagemCadastro.style.display = "none";


        // ========================================================
        // RESTAURAR CLASSE ORIGINAL
        // ========================================================

        // Remove as classes de sucesso ou erro.
        // Mantém somente a classe padrão da mensagem.
        mensagemCadastro.className = "mensagem-cadastro";

    });

}

// ================================================================
// VISUALIZAR SENHA - CADASTRO
// ================================================================

// Localiza o botão responsável por mostrar/esconder a senha.
const btnMostrarCadastroSenha = document.getElementById("btnMostrarCadastroSenha");

// Localiza o campo onde o usuário digita a senha.
const cadastroSenha = document.getElementById("cadastroSenha");

// Localiza o ícone do olhinho da senha.
const iconeCadastroSenha = document.getElementById("iconeCadastroSenha");


// Verifica se os elementos existem na página antes de adicionar o evento.
if (btnMostrarCadastroSenha && cadastroSenha && iconeCadastroSenha) {

    // Executa quando o usuário clicar no botão do olhinho.
    btnMostrarCadastroSenha.addEventListener("click", function () {

        // Verifica se a senha está atualmente escondida.
        if (cadastroSenha.type === "password") {

            // Mostra a senha alterando o tipo do campo para texto.
            cadastroSenha.type = "text";

            // Troca o ícone para indicar que a senha está visível.
            iconeCadastroSenha.classList.remove("bi-eye");

            // Mostra o ícone de olho fechado.
            iconeCadastroSenha.classList.add("bi-eye-slash");

            // Atualiza a descrição do botão para acessibilidade.
            btnMostrarCadastroSenha.setAttribute("aria-label", "Ocultar senha");

        } else {

            // Esconde novamente a senha.
            cadastroSenha.type = "password";

            // Volta o ícone para o olho aberto.
            iconeCadastroSenha.classList.remove("bi-eye-slash");

            // Mostra novamente o ícone de olho aberto.
            iconeCadastroSenha.classList.add("bi-eye");

            // Atualiza a descrição do botão para acessibilidade.
            btnMostrarCadastroSenha.setAttribute("aria-label", "Mostrar senha");
        }

    });

}


// ================================================================
// VISUALIZAR SENHA - CONFIRMAR SENHA
// ================================================================

// Localiza o botão responsável por mostrar/esconder a confirmação da senha.
const btnMostrarConfirmarSenha = document.getElementById("btnMostrarConfirmarSenha");

// Localiza o campo de confirmação da senha.
const cadastroConfirmarSenha = document.getElementById("cadastroConfirmarSenha");

// Localiza o ícone do olhinho da confirmação da senha.
const iconeConfirmarSenha = document.getElementById("iconeConfirmarSenha");


// Verifica se os elementos existem na página antes de adicionar o evento.
if (btnMostrarConfirmarSenha && cadastroConfirmarSenha && iconeConfirmarSenha) {

    // Executa quando o usuário clicar no botão do olhinho.
    btnMostrarConfirmarSenha.addEventListener("click", function () {

        // Verifica se a senha está atualmente escondida.
        if (cadastroConfirmarSenha.type === "password") {

            // Mostra a senha alterando o tipo do campo para texto.
            cadastroConfirmarSenha.type = "text";

            // Troca o ícone para indicar que a senha está visível.
            iconeConfirmarSenha.classList.remove("bi-eye");

            // Mostra o ícone de olho fechado.
            iconeConfirmarSenha.classList.add("bi-eye-slash");

            // Atualiza a descrição do botão para acessibilidade.
            btnMostrarConfirmarSenha.setAttribute("aria-label", "Ocultar senha");

        } else {

            // Esconde novamente a senha.
            cadastroConfirmarSenha.type = "password";

            // Volta o ícone para o olho aberto.
            iconeConfirmarSenha.classList.remove("bi-eye-slash");

            // Mostra novamente o ícone de olho aberto.
            iconeConfirmarSenha.classList.add("bi-eye");

            // Atualiza a descrição do botão para acessibilidade.
            btnMostrarConfirmarSenha.setAttribute("aria-label", "Mostrar senha");
        }

    });

}

// ================================================================
// LOGIN DE USUÁRIO
// ================================================================

// Localiza o formulário de login pelo ID.
const formLogin = document.getElementById("formLogin");


// ================================================================
// VERIFICAÇÃO DO FORMULÁRIO
// ================================================================

// Verifica se o formulário de login existe na página.
if (formLogin) {


    // ============================================================
    // ENVIO DO FORMULÁRIO
    // ============================================================

    // Executa quando o usuário clicar no botão ENTRAR.
    formLogin.addEventListener("submit", async function (event) {

        // Impede o envio tradicional do formulário.
        event.preventDefault();


        // ========================================================
        // PEGAR DADOS DO FORMULÁRIO
        // ========================================================

        // Cria um FormData contendo os dados digitados pelo usuário.
        const dados = new FormData(formLogin);


        try {

            // ====================================================
            // ENVIA OS DADOS PARA O ACCOUNTCONTROLLER
            // ====================================================

            // Envia o e-mail e a senha para a ação Login.
            const resposta = await fetch("/Account/Login", {
                method: "POST",
                body: dados
            });


            // ====================================================
            // DEBUG - RESPOSTA DO SERVIDOR
            // ====================================================

            // Exibe no console o status retornado pelo servidor.
            console.log("Status do login:", resposta.status);

            // Informa se a requisição foi concluída com sucesso.
            console.log("Login OK:", resposta.ok);


            // ====================================================
            // LER RESPOSTA DO SERVIDOR
            // ====================================================

            // Lê a resposta enviada pelo AccountController.
            const textoResposta = await resposta.text();

            // Exibe a resposta no console para facilitar a identificação
            // de possíveis problemas durante os testes.
            console.log("Resposta do login:", textoResposta);


            // ====================================================
            // CONVERTER RESPOSTA PARA JSON
            // ====================================================

            // Cria uma variável para armazenar o resultado.
            let resultado;


            try {

                // Tenta transformar a resposta do servidor em JSON.
                resultado = JSON.parse(textoResposta);

            }
            catch {

                // Caso o servidor retorne algo que não seja JSON,
                // cria uma mensagem de erro padrão.
                resultado = {
                    sucesso: false,
                    mensagem: "O servidor retornou uma resposta inesperada."
                };

            }


            // ====================================================
            // LOGIN REALIZADO COM SUCESSO
            // ====================================================

            // Verifica se o servidor informou que o login foi realizado.
            if (resposta.ok && resultado.sucesso) {

                // Exibe a mensagem de boas-vindas no console.
                console.log(resultado.mensagem);


                // =================================================
                // FECHAR MODAL DE LOGIN
                // =================================================

                // Localiza o modal de login.
                const modalLogin = document.getElementById("modalLogin");


                // Verifica se o modal existe.
                if (modalLogin) {

                    // Obtém a instância do modal do Bootstrap.
                    const instanciaModal =
                        bootstrap.Modal.getInstance(modalLogin);


                    // Se existir uma instância aberta,
                    // fecha o modal.
                    if (instanciaModal) {

                        instanciaModal.hide();

                    }

                }


                // =================================================
                // LIMPAR FORMULÁRIO
                // =================================================

                // Limpa os campos do formulário após o login.
                formLogin.reset();


                // =================================================
                // ATUALIZAR A PÁGINA APÓS O LOGIN
                // =================================================

                // Exibe no console a mensagem retornada pelo servidor.
                console.log("Login realizado:", resultado.mensagem);


                // Recarrega a página.

                // Isso é necessário porque o menu do usuário é
                // renderizado pelo Razor no servidor.

                // Após o recarregamento, o ASP.NET irá identificar
                // o cookie de autenticação criado durante o login.

                // Com isso, User.Identity.IsAuthenticated será true
                // e o menu autenticado será exibido.
                window.location.reload();

            }


            // ====================================================
            // ERRO NO LOGIN
            // ====================================================

            else {

                // Exibe a mensagem de erro no console.
                console.error(
                    resultado.mensagem ||
                    "E-mail ou senha incorretos."
                );

            }

        }
        catch (erro) {

            // ====================================================
            // ERRO DE COMUNICAÇÃO
            // ====================================================

            // Exibe o erro no console.
            console.error("Erro no login:", erro);

        }

    });

}


// ================================================================
// VISUALIZAR SENHA - LOGIN
// ================================================================

// Localiza o botão responsável por mostrar/esconder a senha.
const btnMostrarSenha = document.getElementById("btnMostrarSenha");

// Localiza o campo de senha do login.
const loginSenha = document.getElementById("loginSenha");

// Localiza o ícone do olhinho do login.
const iconeSenha = document.getElementById("iconeSenha");


// ================================================================
// VERIFICAÇÃO DOS ELEMENTOS
// ================================================================

// Verifica se os três elementos existem antes de adicionar o evento.
if (btnMostrarSenha && loginSenha && iconeSenha) {


    // ============================================================
    // CLIQUE NO OLHINHO
    // ============================================================

    // Executa quando o usuário clicar no botão do olhinho.
    btnMostrarSenha.addEventListener("click", function () {


        // ========================================================
        // MOSTRAR SENHA
        // ========================================================

        // Verifica se a senha está escondida.
        if (loginSenha.type === "password") {

            // Altera o tipo do campo para texto.
            loginSenha.type = "text";


            // Remove o ícone de olho aberto.
            iconeSenha.classList.remove("bi-eye");


            // Adiciona o ícone de olho fechado.
            iconeSenha.classList.add("bi-eye-slash");


            // Atualiza o texto de acessibilidade do botão.
            btnMostrarSenha.setAttribute(
                "aria-label",
                "Ocultar senha"
            );

        }


        // ========================================================
        // ESCONDER SENHA
        // ========================================================

        else {

            // Altera novamente o tipo do campo para senha.
            loginSenha.type = "password";


            // Remove o ícone de olho fechado.
            iconeSenha.classList.remove("bi-eye-slash");


            // Adiciona novamente o ícone de olho aberto.
            iconeSenha.classList.add("bi-eye");


            // Atualiza o texto de acessibilidade do botão.
            btnMostrarSenha.setAttribute(
                "aria-label",
                "Mostrar senha"
            );

        }

    });

}


