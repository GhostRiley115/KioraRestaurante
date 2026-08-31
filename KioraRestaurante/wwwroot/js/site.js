// ================================================================
// CADASTRO DE USUÁRIO
// ================================================================

// Localiza o formulário de cadastro pelo ID.
const formCadastro = document.getElementById("formCadastro");

// Localiza a área onde serão exibidas as mensagens de sucesso ou erro.
const mensagemCadastro = document.getElementById("mensagemCadastro");


// ================================================================
// VERIFICAÇÃO DOS ELEMENTOS
// ================================================================

// Verifica se o formulário e a área de mensagem existem na página.
// Essa verificação é importante porque o _Layout.cshtml
// pode ser utilizado em diferentes páginas.
if (formCadastro && mensagemCadastro) {


    // ============================================================
    // ENVIO DO FORMULÁRIO
    // ============================================================

    formCadastro.addEventListener("submit", async function (event) {

        // Impede o comportamento padrão do formulário.
        // Assim, a página não será recarregada.
        event.preventDefault();


        // ========================================================
        // LIMPAR MENSAGEM ANTERIOR
        // ========================================================

        mensagemCadastro.style.display = "none";
        mensagemCadastro.textContent = "";


        // ========================================================
        // PEGAR DADOS DO FORMULÁRIO
        // ========================================================

        // FormData coleta automaticamente:
        // - Nome
        // - Email
        // - Senha
        // - ConfirmarSenha
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
            // LER A RESPOSTA
            // ====================================================

            // Lê a resposta como texto primeiro.
            // Isso evita que um erro de JSON cause um problema
            // antes de conseguirmos identificar o que aconteceu.
            const textoResposta = await resposta.text();

            let resultado;

            try {

                // Tenta transformar a resposta em JSON.
                resultado = JSON.parse(textoResposta);

            }
            catch {

                // Caso o servidor tenha retornado algo que não seja JSON.
                resultado = {
                    sucesso: false,
                    mensagem: "O servidor retornou uma resposta inesperada."
                };

            }


            // ====================================================
            // CADASTRO REALIZADO COM SUCESSO
            // ====================================================

            if (resposta.ok && resultado.sucesso) {

                // Exibe a mensagem retornada pelo Controller.
                mensagemCadastro.textContent = resultado.mensagem;

                // Aplica o estilo de sucesso.
                mensagemCadastro.className =
                    "mensagem-cadastro mensagem-cadastro-sucesso";

                // Mostra a mensagem.
                mensagemCadastro.style.display = "block";


                // Limpa os campos somente após o cadastro
                // ter sido realizado com sucesso.
                formCadastro.reset();

            }


            // ====================================================
            // ERRO NO CADASTRO
            // ====================================================

            else {

                // Exibe a mensagem retornada pelo Controller.
                mensagemCadastro.textContent =
                    resultado.mensagem ||
                    "Não foi possível realizar o cadastro.";

                // Aplica o estilo visual de erro.
                mensagemCadastro.className =
                    "mensagem-cadastro mensagem-cadastro-erro";

                // Mostra a mensagem.
                mensagemCadastro.style.display = "block";

            }

        }
        catch (erro) {

            // ====================================================
            // ERRO DE COMUNICAÇÃO
            // ====================================================

            mensagemCadastro.textContent =
                "Não foi possível realizar o cadastro. Tente novamente.";

            // Aplica o estilo visual de erro.
            mensagemCadastro.className =
                "mensagem-cadastro mensagem-cadastro-erro";

            // Mostra a mensagem.
            mensagemCadastro.style.display = "block";


            // Mostra o erro no console do navegador.
            console.error("Erro no cadastro:", erro);

        }

    });

}

