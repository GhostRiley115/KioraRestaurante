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
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            HttpClient httpClient,
            IConfiguration configuration, ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> EnviarCodigoRecuperacaoSenhaAsync(
            string emailDestino,
            string codigo)
        {
            var apiKey = _configuration["Brevo:ApiKey"];
            var remetenteNome = _configuration["Brevo:RemetenteNome"];
            var remetenteEmail = _configuration["Brevo:RemetenteEmail"];

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("SUA_", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(remetenteEmail))
            {
                _logger.LogWarning("Recuperação de senha indisponível: configure a chave e o remetente da Brevo.");
                return false;
            }

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

            try
            {
                using var resposta = await _httpClient.SendAsync(requisicao);
                if (resposta.IsSuccessStatusCode) return true;
                // Não registra corpo, código de recuperação, destinatário ou credenciais do provedor.
                _logger.LogWarning("Falha no envio de recuperação pela Brevo. HTTP {Status}.", (int)resposta.StatusCode);
                return false;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Falha de comunicação com a Brevo ao enviar recuperação de senha.");
                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tempo de envio de recuperação de senha esgotado.");
                return false;
            }
        }
    }
}
