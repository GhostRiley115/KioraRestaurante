using KioraRestaurante.Models.Enums;
using System.Text.Json.Serialization;

namespace KioraRestaurante.DTOs.Usuario
{
    public class UsuarioResponseDTO
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public TipoUsuario Tipo {  get; set; }
        public bool Ativo { get; set; }

        // Usada internamente para emitir o cookie, sem expor dados de autenticação nas respostas JSON.
        [JsonIgnore]
        public Guid VersaoSessao { get; set; }
    }
}
