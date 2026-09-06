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
                mensagemCadastro.className = "mensagem-kiora mensagem-kiora-sucesso";

                // Garante que a mensagem fique visível.
                mensagemCadastro.style.display = "block";

                // Limpa o formulário após o cadastro.
                formCadastro.reset();

            } else {

                // Mostra a mensagem de erro.
                mensagemCadastro.textContent =
                    resultado.mensagem || "Não foi possível realizar o cadastro.";

                // Adiciona a classe de erro.
                mensagemCadastro.className = "mensagem-kiora mensagem-kiora-erro";

                // Garante que a mensagem fique visível.
                mensagemCadastro.style.display = "block";
            }

        } catch (erro) {

            // Mostra o erro no console.
            console.error("Erro no cadastro:", erro);

            // Mostra mensagem de erro para o usuário.
            mensagemCadastro.textContent =
                "Não foi possível realizar o cadastro. Tente novamente.";

            // Adiciona as classes padrão de erro do Kiora.
            mensagemCadastro.className = "mensagem-kiora mensagem-kiora-erro";

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
                        "mensagem-kiora mensagem-kiora-erro";

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
                    "mensagem-kiora mensagem-kiora-erro";

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

// ================================================================
// LIMPAR MODAL DE LOGIN
// ================================================================

// Obtém o modal de login.
const modalLogin = document.getElementById("modalLogin");

// Verifica se o modal existe.
if (modalLogin) {

    // Executa quando o modal termina de fechar.
    modalLogin.addEventListener("hidden.bs.modal", function () {

        // Limpa os campos do formulário.
        if (formLogin) {
            formLogin.reset();
        }

        // Limpa a mensagem.
        if (mensagemLogin) {
            mensagemLogin.textContent = "";
            mensagemLogin.className = "";
            mensagemLogin.style.display = "none";
        }

        // Retorna o campo de senha para o tipo password.
        if (loginSenha) {
            loginSenha.type = "password";
        }

        // Retorna o ícone para o olho normal.
        if (iconeSenha) {
            iconeSenha.classList.remove("bi-eye-slash");
            iconeSenha.classList.add("bi-eye");
        }

        // Atualiza a descrição do botão.
        if (btnMostrarSenha) {
            btnMostrarSenha.setAttribute(
                "aria-label",
                "Mostrar senha"
            );
        }
    });
}

// ================================================================
// EDITAR PERFIL
// ================================================================

// Localiza o formulário responsável pela edição do perfil.
const formEditarPerfil = document.getElementById("formEditarPerfil");

// Localiza a área onde serão exibidas as mensagens
// de sucesso ou erro.
const mensagemEditarPerfil =
    document.getElementById("mensagemEditarPerfil");


// ================================================================
// FORMULÁRIO DE EDIÇÃO DO PERFIL
// ================================================================

// Verifica se o formulário existe na página.
//
// Essa verificação é importante porque o site.js
// é utilizado em outras páginas do sistema.
// Dessa forma, o código só será executado
// quando o formulário realmente estiver presente.
if (formEditarPerfil) {

    // Detecta o envio do formulário.
    formEditarPerfil.addEventListener("submit", async function (evento) {

        // Impede que o navegador recarregue a página
        // ao enviar o formulário.
        evento.preventDefault();


        // ============================================================
        // LIMPAR MENSAGEM ANTERIOR
        // ============================================================

        // Remove a mensagem exibida anteriormente.
        mensagemEditarPerfil.textContent = "";

        // Esconde a área de mensagem.
        mensagemEditarPerfil.style.display = "none";


        // ============================================================
        // CAPTURAR OS DADOS DO FORMULÁRIO
        // ============================================================

        // Cria um objeto FormData utilizando
        // os campos existentes no formulário.
        const dados = new FormData(formEditarPerfil);


        // ============================================================
        // ENVIAR DADOS PARA O CONTROLLER
        // ============================================================

        try {

            // Envia os dados para a ação EditarPerfil
            // do AccountController.
            const resposta = await fetch("/Account/EditarPerfil", {
                method: "POST",
                body: dados
            });


            // Converte a resposta recebida do Controller
            // para um objeto JavaScript.
            const resultado = await resposta.json();


            // ========================================================
            // VERIFICAR RESULTADO
            // ========================================================

            // Verifica se o Controller informou
            // que a operação foi realizada com sucesso.
            if (resultado.sucesso) {

                // Exibe a mensagem de sucesso.
                mensagemEditarPerfil.textContent =
                    resultado.mensagem;

                // Exibe a área da mensagem.
                mensagemEditarPerfil.style.display = "block";


                // Define uma cor verde para indicar
                // que a operação foi concluída.
                mensagemEditarPerfil.style.color = "#FFFFFF";

                mensagemEditarPerfil.style.backgroundColor =
                    "#198754";


                // ====================================================
                // ATUALIZAR NOME DO MENU
                // ====================================================

                // Procura o elemento que exibe o nome
                // do usuário no menu superior.
                const nomeUsuarioMenu =
                    document.querySelector(".nome-usuario-menu");


                // Verifica se o elemento foi encontrado
                // e se o Controller enviou um novo nome.
                if (nomeUsuarioMenu && resultado.nome) {

                    // Atualiza o nome exibido no menu
                    // sem precisar recarregar a página.
                    nomeUsuarioMenu.textContent =
                        resultado.nome;
                }


                // ====================================================
                // FECHAR MODAL
                // ====================================================

                // Aguarda um pequeno intervalo para que
                // o usuário consiga visualizar a mensagem
                // de sucesso antes do modal ser fechado.
                setTimeout(function () {

                    // Localiza o modal de edição.
                    const modalElemento =
                        document.getElementById("modalEditarPerfil");


                    // Verifica se o modal existe.
                    if (modalElemento) {

                        // Obtém a instância do modal criada
                        // pelo Bootstrap.
                        const modal =
                            bootstrap.Modal.getInstance(
                                modalElemento
                            );


                        // Se a instância existir,
                        // fecha o modal.
                        if (modal) {
                            modal.hide();
                        }
                    }


                    // =================================================
                    // ATUALIZAR DADOS EXIBIDOS NA PÁGINA
                    // =================================================

                    // Atualiza o nome exibido no card do perfil.
                    //
                    // A página ainda possui os dados antigos
                    // porque não foi recarregada.
                    //
                    // Para evitar alterações desnecessárias
                    // no HTML atual, recarregaremos a página
                    // depois que o modal for fechado.
                    window.location.reload();

                }, 1200);

            }

            // ========================================================
            // ERRO
            // ========================================================

            else {

                // Exibe a mensagem enviada pelo Controller.
                mensagemEditarPerfil.textContent =
                    resultado.mensagem ||
                    "Não foi possível atualizar o perfil.";

                // Exibe a área da mensagem.
                mensagemEditarPerfil.style.display = "block";


                // Define uma aparência de erro.
                mensagemEditarPerfil.style.color =
                    "#FFFFFF";

                mensagemEditarPerfil.style.backgroundColor =
                    "#7D1F1F";
            }


        }

        // ============================================================
        // ERRO DE COMUNICAÇÃO
        // ============================================================

        catch (erro) {

            // Exibe uma mensagem caso ocorra
            // algum problema na comunicação com o servidor.
            mensagemEditarPerfil.textContent =
                "Ocorreu um erro ao atualizar o perfil.";

            // Exibe a mensagem.
            mensagemEditarPerfil.style.display = "block";


            // Define a aparência de erro.
            mensagemEditarPerfil.style.color =
                "#FFFFFF";

            mensagemEditarPerfil.style.backgroundColor =
                "#7D1F1F";


            // Registra o erro no console
            // para facilitar a identificação do problema.
            console.error(
                "Erro ao editar perfil:",
                erro
            );
        }

    });
}

// ================================================================
// ALTERAR SENHA
// ================================================================

// Localiza o formulário de alteração de senha.
const formAlterarSenha =
    document.getElementById("formAlterarSenha");

// Localiza a área onde serão exibidas
// as mensagens de sucesso ou erro.
const mensagemAlterarSenha =
    document.getElementById("mensagemAlterarSenha");

// Verifica se o formulário existe na página.
if (formAlterarSenha) {

    // ============================================================
    // ENVIO DO FORMULÁRIO
    // ============================================================

    formAlterarSenha.addEventListener(
        "submit",
        async function (evento) {

            // Impede o envio tradicional do formulário.
            evento.preventDefault();

            // Limpa qualquer mensagem anterior.
            mensagemAlterarSenha.textContent = "";

            // Esconde a mensagem anterior.
            mensagemAlterarSenha.style.display = "none";

            // Cria os dados do formulário.
            const dados = new FormData(formAlterarSenha);

            try {

                // Envia os dados para o AccountController.
                const resposta = await fetch(
                    "/Account/AlterarSenha",
                    {
                        method: "POST",
                        body: dados
                    }
                );

                // Converte a resposta para JSON.
                const resultado = await resposta.json();

                // ====================================================
                // SENHA ALTERADA COM SUCESSO
                // ====================================================

                if (resultado.sucesso) {

                    // Exibe a mensagem recebida do Controller.
                    mensagemAlterarSenha.textContent =
                        resultado.mensagem;

                    // Mostra a mensagem.
                    mensagemAlterarSenha.style.display =
                        "block";

                    // Define a cor do texto.
                    mensagemAlterarSenha.style.color =
                        "#FFFFFF";

                    // Define o fundo verde de sucesso.
                    mensagemAlterarSenha.style.backgroundColor =
                        "#198754";

                    // Limpa os campos do formulário.
                    formAlterarSenha.reset();

                    // Aguarda um pouco e fecha o modal.
                    setTimeout(function () {

                        const modalElemento =
                            document.getElementById(
                                "modalAlterarSenha"
                            );

                        if (modalElemento) {

                            const modal =
                                bootstrap.Modal.getInstance(
                                    modalElemento
                                );

                            if (modal) {
                                modal.hide();
                            }
                        }

                    }, 1200);

                }

                // ====================================================
                // ERRO NA ALTERAÇÃO DA SENHA
                // ====================================================

                else {

                    // Exibe a mensagem de erro.
                    mensagemAlterarSenha.textContent =
                        resultado.mensagem ||
                        "Não foi possível alterar a senha.";

                    // Mostra a mensagem.
                    mensagemAlterarSenha.style.display =
                        "block";

                    // Define a cor do texto.
                    mensagemAlterarSenha.style.color =
                        "#FFFFFF";

                    // Define o fundo vinho de erro.
                    mensagemAlterarSenha.style.backgroundColor =
                        "#7D1F1F";
                }

            }

            // ========================================================
            // ERRO DE COMUNICAÇÃO
            // ========================================================

            catch (erro) {

                // Exibe uma mensagem para o usuário.
                mensagemAlterarSenha.textContent =
                    "Ocorreu um erro ao alterar a senha.";

                // Mostra a mensagem.
                mensagemAlterarSenha.style.display =
                    "block";

                // Define a cor do texto.
                mensagemAlterarSenha.style.color =
                    "#FFFFFF";

                // Define o fundo vinho de erro.
                mensagemAlterarSenha.style.backgroundColor =
                    "#7D1F1F";

                // Exibe o erro no console
                // para facilitar a identificação do problema.
                console.error(
                    "Erro ao alterar senha:",
                    erro
                );
            }

        }
    );
}

// ================================================================
// MOSTRAR / OCULTAR SENHA ATUAL
// ================================================================

// Localiza o botão responsável por mostrar ou ocultar
// a senha atual.
const btnMostrarSenhaAtual =
    document.getElementById("btnMostrarSenhaAtual");

// Localiza o campo da senha atual.
const senhaAtual =
    document.getElementById("senhaAtual");

// Localiza o ícone do olho.
const iconeSenhaAtual =
    document.getElementById("iconeSenhaAtual");

// Verifica se os elementos existem na página.
if (
    btnMostrarSenhaAtual &&
    senhaAtual &&
    iconeSenhaAtual
) {

    btnMostrarSenhaAtual.addEventListener(
        "click",
        function () {

            // Verifica se a senha está escondida.
            if (senhaAtual.type === "password") {

                // Mostra a senha.
                senhaAtual.type = "text";

                // Altera o ícone para olho fechado.
                iconeSenhaAtual.classList.remove(
                    "bi-eye"
                );

                iconeSenhaAtual.classList.add(
                    "bi-eye-slash"
                );

                // Atualiza a descrição do botão.
                btnMostrarSenhaAtual.setAttribute(
                    "aria-label",
                    "Ocultar senha"
                );

            } else {

                // Esconde novamente a senha.
                senhaAtual.type = "password";

                // Altera o ícone para olho aberto.
                iconeSenhaAtual.classList.remove(
                    "bi-eye-slash"
                );

                iconeSenhaAtual.classList.add(
                    "bi-eye"
                );

                // Atualiza a descrição do botão.
                btnMostrarSenhaAtual.setAttribute(
                    "aria-label",
                    "Mostrar senha"
                );
            }

        }
    );
}

// ================================================================
// MOSTRAR / OCULTAR NOVA SENHA
// ================================================================

// Localiza o botão responsável por mostrar ou ocultar
// a nova senha.
const btnMostrarNovaSenha =
    document.getElementById("btnMostrarNovaSenha");

// Localiza o campo da nova senha.
const novaSenha =
    document.getElementById("novaSenha");

// Localiza o ícone do olho.
const iconeNovaSenha =
    document.getElementById("iconeNovaSenha");

// Verifica se os elementos existem na página.
if (
    btnMostrarNovaSenha &&
    novaSenha &&
    iconeNovaSenha
) {

    btnMostrarNovaSenha.addEventListener(
        "click",
        function () {

            // Verifica se a senha está escondida.
            if (novaSenha.type === "password") {

                // Mostra a senha.
                novaSenha.type = "text";

                // Altera o ícone para olho fechado.
                iconeNovaSenha.classList.remove(
                    "bi-eye"
                );

                iconeNovaSenha.classList.add(
                    "bi-eye-slash"
                );

                // Atualiza a descrição do botão.
                btnMostrarNovaSenha.setAttribute(
                    "aria-label",
                    "Ocultar senha"
                );

            } else {

                // Esconde novamente a senha.
                novaSenha.type = "password";

                // Altera o ícone para olho aberto.
                iconeNovaSenha.classList.remove(
                    "bi-eye-slash"
                );

                iconeNovaSenha.classList.add(
                    "bi-eye"
                );

                // Atualiza a descrição do botão.
                btnMostrarNovaSenha.setAttribute(
                    "aria-label",
                    "Mostrar senha"
                );
            }

        }
    );
}

// ================================================================
// MOSTRAR / OCULTAR CONFIRMAÇÃO DA NOVA SENHA
// ================================================================

// Localiza o botão responsável por mostrar ou ocultar
// a confirmação da nova senha.
const btnMostrarConfirmarNovaSenha =
    document.getElementById("btnMostrarConfirmarNovaSenha");

// Localiza o campo de confirmação da nova senha.
const confirmarNovaSenha =
    document.getElementById("confirmarNovaSenha");

// Localiza o ícone do olho.
const iconeConfirmarNovaSenha =
    document.getElementById("iconeConfirmarNovaSenha");

// Verifica se os elementos existem na página.
if (
    btnMostrarConfirmarNovaSenha &&
    confirmarNovaSenha &&
    iconeConfirmarNovaSenha
) {

    btnMostrarConfirmarNovaSenha.addEventListener(
        "click",
        function () {

            // Verifica se a senha está escondida.
            if (confirmarNovaSenha.type === "password") {

                // Mostra a senha.
                confirmarNovaSenha.type = "text";

                // Altera o ícone para olho fechado.
                iconeConfirmarNovaSenha.classList.remove(
                    "bi-eye"
                );

                iconeConfirmarNovaSenha.classList.add(
                    "bi-eye-slash"
                );

                // Atualiza a descrição do botão.
                btnMostrarConfirmarNovaSenha.setAttribute(
                    "aria-label",
                    "Ocultar senha"
                );

            } else {

                // Esconde novamente a senha.
                confirmarNovaSenha.type = "password";

                // Altera o ícone para olho aberto.
                iconeConfirmarNovaSenha.classList.remove(
                    "bi-eye-slash"
                );

                iconeConfirmarNovaSenha.classList.add(
                    "bi-eye"
                );

                // Atualiza a descrição do botão.
                btnMostrarConfirmarNovaSenha.setAttribute(
                    "aria-label",
                    "Mostrar senha"
                );
            }

        }
    );
}

// ================================================================
// RECUPERAÇÃO DE SENHA
// ================================================================

// Localiza o formulário de recuperação de senha.
const formEsqueciSenha = document.getElementById("formEsqueciSenha");

// Localiza a área onde serão exibidas as mensagens.
const mensagemEsqueciSenha = document.getElementById("mensagemEsqueciSenha");


// Verifica se o formulário existe na página.
if (formEsqueciSenha) {

    // Detecta o envio do formulário.
    formEsqueciSenha.addEventListener("submit", async function (event) {

        // Impede o comportamento padrão do formulário.
        event.preventDefault();


        // ============================================================
        // LIMPAR MENSAGEM ANTERIOR
        // ============================================================

        mensagemEsqueciSenha.style.display = "none";
        mensagemEsqueciSenha.innerHTML = "";


        // ============================================================
        // PEGAR DADOS DO FORMULÁRIO
        // ============================================================

        const dados = new FormData(formEsqueciSenha);


        try {

            // ========================================================
            // ENVIAR SOLICITAÇÃO
            // ========================================================

            const resposta = await fetch("/Account/EsqueciSenha", {
                method: "POST",
                body: dados
            });


            // Converte a resposta para JSON.
            const resultado = await resposta.json();


            // ========================================================
            // EXIBIR MENSAGEM
            // ========================================================

            mensagemEsqueciSenha.innerHTML = resultado.mensagem;

            mensagemEsqueciSenha.style.display = "block";


        } catch (erro) {

            // ========================================================
            // ERRO DE COMUNICAÇÃO
            // ========================================================

            mensagemEsqueciSenha.innerHTML =
                "Não foi possível realizar a solicitação. Tente novamente.";

            mensagemEsqueciSenha.style.display = "block";

            console.error(
                "Erro na recuperação de senha:",
                erro
            );
        }

    });
}

