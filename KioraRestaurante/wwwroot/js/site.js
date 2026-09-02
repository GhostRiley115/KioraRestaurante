// ================================================================
// CADASTRO
// ================================================================

// Obtém o formulário de cadastro.
const formCadastro = document.getElementById("formCadastro");

// Obtém o elemento responsável por mostrar as mensagens do cadastro.
const mensagemCadastro = document.getElementById("mensagemCadastro");


// Verifica se o formulário de cadastro existe na página.
if (formCadastro) {

    // Captura o envio do formulário.
    formCadastro.addEventListener("submit", async function (event) {

        // Impede o formulário de recarregar a página.
        event.preventDefault();

        // Limpa mensagens anteriores.
        mensagemCadastro.textContent = "";

        // Cria os dados do formulário.
        const dados = new FormData(formCadastro);

        try {

            // Envia os dados para o Controller.
            const resposta = await fetch("/Account/Cadastro", {
                method: "POST",
                body: dados
            });

            // Converte a resposta para JSON.
            const resultado = await resposta.json();

            // Verifica se o cadastro foi realizado.
            if (resposta.ok && resultado.sucesso) {

                // Mostra a mensagem de sucesso.
                mensagemCadastro.textContent = resultado.mensagem;

                // Adiciona a classe de sucesso.
                mensagemCadastro.className = "mensagem-cadastro mensagem-cadastro-sucesso";

                // Garante que a mensagem fique visível.
                mensagemCadastro.style.display = "block";

                // Limpa o formulário após o cadastro.
                formCadastro.reset();

            } else {

                // Mostra a mensagem de erro.
                mensagemCadastro.textContent =
                    resultado.mensagem || "Não foi possível realizar o cadastro.";

                // Adiciona a classe de erro.
                mensagemCadastro.className = "mensagem-cadastro mensagem-cadastro-erro";

                // Garante que a mensagem fique visível.
                mensagemCadastro.style.display = "block";
            }

        } catch (erro) {

            // Mostra o erro no console.
            console.error("Erro no cadastro:", erro);

            // Mostra mensagem de erro para o usuário.
            mensagemCadastro.textContent =
                "Não foi possível realizar o cadastro. Tente novamente.";

            // Adiciona a classe de erro.
            mensagemCadastro.className = "mensagem-cadastro mensagem-cadastro-erro";

            // Garante que a mensagem fique visível.
            mensagemCadastro.style.display = "block";
        }
    });
}


// ================================================================
// LIMPAR MODAL DE CADASTRO
// ================================================================

// Obtém o modal de cadastro.
const modalCadastro = document.getElementById("modalCadastro");

// Verifica se o modal existe.
if (modalCadastro) {

    // Executa quando o modal termina de fechar.
    modalCadastro.addEventListener("hidden.bs.modal", function () {

        // Limpa os campos do formulário.
        if (formCadastro) {
            formCadastro.reset();
        }

        // Limpa a mensagem.
        if (mensagemCadastro) {
            mensagemCadastro.textContent = "";
            mensagemCadastro.className = "";
            mensagemCadastro.style.display = "none";
        }
    });
}


// ================================================================
// MOSTRAR / OCULTAR SENHA DO CADASTRO
// ================================================================

// Obtém o botão de mostrar senha.
const btnMostrarCadastroSenha =
    document.getElementById("btnMostrarCadastroSenha");

// Obtém o campo de senha.
const cadastroSenha =
    document.getElementById("cadastroSenha");

// Obtém o ícone da senha.
const iconeCadastroSenha =
    document.getElementById("iconeCadastroSenha");


// Verifica se os elementos existem.
if (btnMostrarCadastroSenha && cadastroSenha && iconeCadastroSenha) {

    // Captura o clique no botão.
    btnMostrarCadastroSenha.addEventListener("click", function () {

        // Verifica se a senha está escondida.
        if (cadastroSenha.type === "password") {

            // Mostra a senha.
            cadastroSenha.type = "text";

            // Altera o ícone.
            iconeCadastroSenha.classList.remove("bi-eye");
            iconeCadastroSenha.classList.add("bi-eye-slash");

            // Atualiza a descrição do botão.
            btnMostrarCadastroSenha.setAttribute(
                "aria-label",
                "Ocultar senha"
            );

        } else {

            // Esconde a senha.
            cadastroSenha.type = "password";

            // Altera o ícone.
            iconeCadastroSenha.classList.remove("bi-eye-slash");
            iconeCadastroSenha.classList.add("bi-eye");

            // Atualiza a descrição do botão.
            btnMostrarCadastroSenha.setAttribute(
                "aria-label",
                "Mostrar senha"
            );
        }
    });
}


// ================================================================
// MOSTRAR / OCULTAR CONFIRMAÇÃO DE SENHA
// ================================================================

// Obtém o botão da confirmação de senha.
const btnMostrarConfirmarSenha =
    document.getElementById("btnMostrarConfirmarSenha");

// Obtém o campo de confirmação de senha.
const cadastroConfirmarSenha =
    document.getElementById("cadastroConfirmarSenha");

// Obtém o ícone da confirmação de senha.
const iconeConfirmarSenha =
    document.getElementById("iconeConfirmarSenha");


// Verifica se os elementos existem.
if (
    btnMostrarConfirmarSenha &&
    cadastroConfirmarSenha &&
    iconeConfirmarSenha
) {

    // Captura o clique no botão.
    btnMostrarConfirmarSenha.addEventListener("click", function () {

        // Verifica se a senha está escondida.
        if (cadastroConfirmarSenha.type === "password") {

            // Mostra a senha.
            cadastroConfirmarSenha.type = "text";

            // Altera o ícone.
            iconeConfirmarSenha.classList.remove("bi-eye");
            iconeConfirmarSenha.classList.add("bi-eye-slash");

            // Atualiza a descrição do botão.
            btnMostrarConfirmarSenha.setAttribute(
                "aria-label",
                "Ocultar senha"
            );

        } else {

            // Esconde a senha.
            cadastroConfirmarSenha.type = "password";

            // Altera o ícone.
            iconeConfirmarSenha.classList.remove("bi-eye-slash");
            iconeConfirmarSenha.classList.add("bi-eye");

            // Atualiza o ícone.
            iconeConfirmarSenha.classList.add("bi-eye");

            // Atualiza a descrição do botão.
            btnMostrarConfirmarSenha.setAttribute(
                "aria-label",
                "Mostrar senha"
            );
        }
    });
}


// ================================================================
// LOGIN
// ================================================================

// Obtém o formulário de login.
const formLogin = document.getElementById("formLogin");

// Obtém o elemento responsável pela mensagem de login.
const mensagemLogin = document.getElementById("mensagemLogin");


// Verifica se o formulário existe.
if (formLogin) {

    // Captura o envio do formulário.
    formLogin.addEventListener("submit", async function (event) {

        // Impede o comportamento padrão do formulário.
        event.preventDefault();

        // Limpa mensagem anterior.
        if (mensagemLogin) {
            mensagemLogin.textContent = "";
            mensagemLogin.className = "";
            mensagemLogin.style.display = "none";
        }

        // Cria os dados do formulário.
        const dados = new FormData(formLogin);

        try {

            // Envia os dados para o Controller.
            const resposta = await fetch("/Account/Login", {
                method: "POST",
                body: dados
            });

            // Mostra o status no console.
            console.log("Status do login:", resposta.status);

            // Lê a resposta como texto.
            const textoResposta = await resposta.text();

            // Mostra a resposta no console.
            console.log("Resposta do login:", textoResposta);

            // Converte a resposta para JSON.
            const resultado = JSON.parse(textoResposta);

            // Mostra o resultado no console.
            console.log("Login OK:", resultado.sucesso);

            // Verifica se o login foi realizado com sucesso.
            if (resposta.ok && resultado.sucesso) {

                // Mostra a mensagem no console.
                console.log("Login realizado:", resultado.mensagem);

                // Obtém o modal de login.
                const modalLogin =
                    document.getElementById("modalLogin");

                // Verifica se o Bootstrap está disponível.
                if (modalLogin && typeof bootstrap !== "undefined") {

                    // Obtém a instância do modal.
                    const instanciaModal =
                        bootstrap.Modal.getInstance(modalLogin);

                    // Fecha o modal.
                    if (instanciaModal) {
                        instanciaModal.hide();
                    }
                }

                // Limpa o formulário.
                formLogin.reset();

                // Recarrega a página para atualizar o menu.
                window.location.reload();

            } else {

                // Obtém a mensagem enviada pelo servidor.
                const mensagemErro =
                    resultado.mensagem ||
                    "E-mail ou senha incorretos.";

                // Mostra o erro no console.
                console.error(mensagemErro);

                // Mostra a mensagem para o usuário.
                if (mensagemLogin) {

                    // Define o texto.
                    mensagemLogin.textContent = mensagemErro;

                    // Define as classes.
                    mensagemLogin.className =
                        "mensagem-login mensagem-login-erro";

                    // Mostra a mensagem.
                    mensagemLogin.style.display = "block";
                }
            }

        } catch (erro) {

            // Mostra o erro no console.
            console.error("Erro no login:", erro);

            // Mostra mensagem de erro para o usuário.
            if (mensagemLogin) {

                // Define o texto.
                mensagemLogin.textContent =
                    "Não foi possível realizar o login. Tente novamente.";

                // Define as classes.
                mensagemLogin.className =
                    "mensagem-login mensagem-login-erro";

                // Mostra a mensagem.
                mensagemLogin.style.display = "block";
            }
        }
    });
}


// ================================================================
// MOSTRAR / OCULTAR SENHA DO LOGIN
// ================================================================

// Obtém o botão de mostrar senha.
const btnMostrarSenha =
    document.getElementById("btnMostrarSenha");

// Obtém o campo de senha.
const loginSenha =
    document.getElementById("loginSenha");

// Obtém o ícone da senha.
const iconeSenha =
    document.getElementById("iconeSenha");


// Verifica se os elementos existem.
if (btnMostrarSenha && loginSenha && iconeSenha) {

    // Captura o clique no botão.
    btnMostrarSenha.addEventListener("click", function () {

        // Verifica se a senha está escondida.
        if (loginSenha.type === "password") {

            // Mostra a senha.
            loginSenha.type = "text";

            // Altera o ícone.
            iconeSenha.classList.remove("bi-eye");
            iconeSenha.classList.add("bi-eye-slash");

            // Atualiza a descrição do botão.
            btnMostrarSenha.setAttribute(
                "aria-label",
                "Ocultar senha"
            );

        } else {

            // Esconde a senha.
            loginSenha.type = "password";

            // Altera o ícone.
            iconeSenha.classList.remove("bi-eye-slash");
            iconeSenha.classList.add("bi-eye");

            // Atualiza a descrição do botão.
            btnMostrarSenha.setAttribute(
                "aria-label",
                "Mostrar senha"
            );
        }
    });
}