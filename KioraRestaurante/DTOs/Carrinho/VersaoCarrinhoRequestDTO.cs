using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Carrinho;

public class VersaoCarrinhoRequestDTO
{
    // A alteração precisa corresponder ao carrinho que o cliente viu na tela.
    [Required(ErrorMessage = "Atualize o carrinho antes de alterá-lo.")]
    public Guid? VersaoEsperada { get; set; }
}
