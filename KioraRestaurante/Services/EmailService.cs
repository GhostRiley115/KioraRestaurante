using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Services.Interfaces;

namespace KioraRestaurante.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public EmailService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task EnviarCodigoRecuperacaoSenhaAsync(
            string emailDestino,
            string codigo)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var remetenteNome = _configuration["Brevo:RemetenteNome"];
            var remetenteEmail = _configuration["Brevo:RemetenteEmail"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "A API Key da Brevo não foi configurada.");

            if (string.IsNullOrWhiteSpace(remetenteEmail))
                throw new InvalidOperationException(
                    "O e-mail remetente da Brevo não foi configurado.");

            var dadosEmail = new
            {
                sender = new
                {
                    name = remetenteNome,
                    email = remetenteEmail
                },

                to = new[]
                {
                    new
                    {
                        email = emailDestino
                    }
                },

                subject = "Código para recuperação de senha - Kiora",

                htmlContent = $"""
                    <html>
                    <body>
                        <h2>Kiora Restaurante</h2>

                        <p>Olá!</p>

                        <p>
                            Recebemos uma solicitação para recuperar
                            a senha da sua conta.
                        </p>

                        <p>
                            Seu código de confirmação é:
                        </p>

                        <h1>{codigo}</h1>

                        <p>
                            Digite esse código na página de recuperação
                            de senha do Kiora.
                        </p>

                        <p>
                            Se você não solicitou a recuperação da senha,
                            ignore este e-mail.
                        </p>

                        <p>
                            Atenciosamente,<br>
                            Kiora Restaurante
                        </p>
                    </body>
                    </html>
                    """
            };

            var json = JsonSerializer.Serialize(dadosEmail);

            using var requisicao = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.brevo.com/v3/smtp/email");

            requisicao.Headers.Add("api-key", apiKey);

            requisicao.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var resposta = await _httpClient.SendAsync(requisicao);

            if (!resposta.IsSuccessStatusCode)
            {
                var erro = await resposta.Content.ReadAsStringAsync();

                throw new InvalidOperationException(
                    $"Erro ao enviar e-mail pela Brevo. " +
                    $"Status: {(int)resposta.StatusCode}. " +
                    $"Detalhes: {erro}");
            }
        }
    }
}
