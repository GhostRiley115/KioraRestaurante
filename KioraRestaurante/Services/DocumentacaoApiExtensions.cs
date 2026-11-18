using KioraRestaurante.Filters;
namespace KioraRestaurante.Services;

public static class DocumentacaoApiExtensions
{
    public static IServiceCollection AddDocumentacaoApiKiora(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        // Configura a documentação da API para aceitar autenticação JWT.
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<DocumentarAutenticacaoApi>();
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Description = "Informe o token JWT para autenticar as requisições."
            });

            options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            });
        });

        return services;
    }
}
