using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Imagens;

namespace KioraRestaurante.Services;

public class ImagemProdutoService : IImagemProdutoService
{
    private const long TamanhoMaximo = 5 * 1024 * 1024;

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagemProdutoService> _logger;

    public ImagemProdutoService(IConfiguration configuration, ILogger<ImagemProdutoService> logger,
        IServiceScopeFactory scopes)
    {
        _configuration = configuration;
        _logger = logger;
        _scopes = scopes;
    }

    private static void ValidarArquivo(IFormFile foto)
    {
        if (foto == null || foto.Length == 0)
        {
            throw new RegraProdutoException(
                "Selecione uma foto para o produto.");
        }

        if (foto.Length > TamanhoMaximo)
        {
            throw new RegraProdutoException(
                "A foto deve ter no máximo 5 MB.");
        }

        var extensao = Path.GetExtension(foto.FileName)
            .ToLowerInvariant();

        var permitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (!permitidas.Contains(extensao))
        {
            throw new RegraProdutoException(
                "Envie uma foto JPG, PNG ou WebP.");
        }
    }

    private Cloudinary CriarCliente()
    {
        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName)
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new RegraProdutoException(
                "O armazenamento de imagens ainda não foi configurado.");
        }

        var conta = new Account(cloudName, apiKey, apiSecret);

        var cliente = new Cloudinary(conta);
        cliente.Api.Secure = true;
        cliente.Api.Timeout = 30_000;

        return cliente;
    }

    private async Task RegistrarUploadParaConferencia(string publicId)
    {
        // Salva somente a tarefa, sem antecipar alterações do produto.
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LimpezasImagensPendentes.Add(new LimpezaImagemPendente
        {
            PublicId = publicId,
            // Não confunde um upload/cadastro em andamento com imagem órfã.
            ProximaTentativaUtc = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();
    }

    public async Task<bool> Excluir(string publicId)
    {
        // Impede usar este método para apagar logos, home ou outras pastas.
        if (!IdentificadorImagemProduto.Gerenciado(publicId))
            return false;

        try
        {
            var cliente = CriarCliente();
            var resultado = await cliente.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image,
                Type = "upload",
                Invalidate = true
            });

            // Repetir uma tarefa já concluída é seguro.
            if (resultado.Error == null
                && (resultado.Result == "ok" || resultado.Result == "not found"))
                return true;

            _logger.LogWarning("Cloudinary não confirmou exclusão de {PublicId}.", publicId);
        }
        catch (Exception ex)
        {
            // Uma falha de limpeza não pode desfazer um cadastro confirmado.
            _logger.LogWarning("Falha ao excluir {PublicId}: {Tipo}.",
                publicId, ex.GetType().Name);
        }
        return false;
    }

    public async Task<ImagemProdutoResultado> Enviar(IFormFile foto)
    {
        ValidarArquivo(foto);

        var cliente = CriarCliente();

        // Geramos o identificador para não depender do nome original.
        var publicId = $"kiora/produtos/{Guid.NewGuid():N}";

        // Registra antes de enviar para não perder o identificador se houver timeout.
        await RegistrarUploadParaConferencia(publicId);

        var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();

        using var stream = foto.OpenReadStream();

        var parametros = new ImageUploadParams
        {
            File = new FileDescription($"produto{extensao}", stream),

            PublicId = publicId,
            Overwrite = false,

            // O Cloudinary também valida o formato real recebido.
            AllowedFormats = new[] { "jpg", "png", "webp" },

            // Reduz imagens grandes sem ampliar as que já são menores.
            Transformation = new Transformation()
                .Width(1600)
                .Height(1600)
                .Crop("limit")
        };

        ImageUploadResult resultado;

        try
        {
            resultado = await cliente.UploadAsync(parametros);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Falha de comunicação durante envio da imagem {PublicId}.", publicId);

            throw new RegraProdutoException("Não foi possível enviar a foto. Tente novamente.");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Tempo esgotado durante envio da imagem {PublicId}.", publicId);

            throw new RegraProdutoException("O envio da foto demorou demais. Tente novamente.");
        }

        if (resultado.Error != null || resultado.SecureUrl == null)
        {
            _logger.LogWarning("O Cloudinary não confirmou o envio da imagem {PublicId}.", publicId);

            throw new RegraProdutoException(
                "Não foi possível processar a foto. Confira o arquivo e tente novamente.");
        }

        return new ImagemProdutoResultado
        {
            Url = resultado.SecureUrl.ToString(),
            PublicId = resultado.PublicId
        };
    }
}