using Microsoft.AspNetCore.Http;

namespace KioraRestaurante.Services.Interfaces;

public interface IImagemProdutoService
{
    Task<ImagemProdutoResultado> Enviar(IFormFile foto);
}