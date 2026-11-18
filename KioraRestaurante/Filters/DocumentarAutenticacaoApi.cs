using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KioraRestaurante.Filters;

// As consultas públicas e o login não exigem um token para gerar outro token.
public sealed class DocumentarAutenticacaoApi : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata=context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if(metadata.OfType<IAllowAnonymous>().Any()||!metadata.OfType<IAuthorizeData>().Any())
            operation.Security=[];
    }
}
