using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioLoginDTO
    {
        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        //Verifica se email possuiu @ 
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Senha { get; set; } = string.Empty;
    }
}
