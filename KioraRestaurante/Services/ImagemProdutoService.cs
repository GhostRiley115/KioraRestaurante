using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KioraRestaurante.Services;

public class ImagemProdutoService : IImagemProdutoService
{
    private const long TamanhoMaximo = 5 * 1024 * 1024;

    private readonly IConfiguration _configuration;
    private readonly ILogger<ImagemProdutoService> _logger;

    public ImagemProdutoService(IConfiguration configuration, ILogger<ImagemProdutoService> logger)
    {
        _configuration = configuration;
        _logger = logger;
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

        return cliente;
    }

    public async Task<ImagemProdutoResultado> Enviar(IFormFile foto)
    {
        ValidarArquivo(foto);

        var cliente = CriarCliente();

        // Geramos o identificador para não depender do nome original.
        var publicId = $"kiora/produtos/{Guid.NewGuid():N}";

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