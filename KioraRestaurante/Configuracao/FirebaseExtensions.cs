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
        // Carrega a seção Firebase do appsettings e permite sobrescrever os valores utilizando variáveis de ambiente.

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

        // Uma única instância durante a execução da aplicação.

        services.AddSingleton<
            IEnvioPushService,
            FirebasePushService>();

        return services;
    }
}
