using KioraRestaurante.Options;
using KioraRestaurante.Services.Notificacoes;

namespace KioraRestaurante.Configuracao;

// Configuração centralizada da integração do Kiora Restaurante com o Firebase.
public static class FirebaseExtensions
{
    public static IServiceCollection AddFirebaseKiora(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurações do Firebase.

        services.AddOptions<FirebaseOptions>()
            .Bind(
                configuration.GetSection(
                    FirebaseOptions.Secao))

            .Validate(
                options =>
                    !options.Ativado ||
                    !string.IsNullOrWhiteSpace(
                        options.ProjetoId),

                "Informe Firebase:ProjetoId " +
                "quando a integração estiver ativada.")

            .ValidateOnStart();

        // Comunicação com o Firebase.

        services.AddSingleton<
            IEnvioPushService,
            FirebasePushService>();

        // Inclusão das notificações na fila.

        services.AddScoped<FilaPushService>();

        // Processamento e segurança dos envios.

        services.AddScoped<ProcessadorPushService>();

        // Processamento automático em segundo plano.

        services.AddHostedService<PushBackgroundService>();

        return services;
    }
}
