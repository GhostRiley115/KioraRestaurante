using Microsoft.AspNetCore.Http;

namespace KioraRestaurante.Services.Imagens;

public interface IImagemProdutoService
{
    Task<ImagemProdutoResultado> Enviar(IFormFile foto);
    Task<bool> Excluir(string publicId);
}