using System.ComponentModel.DataAnnotations;

namespace KioraRestaurante.Models;

public class EnderecoUsuario
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [Required, MaxLength(40)]
    public string Apelido { get; set; } = string.Empty;

    [Required, MaxLength(9)]
    public string Cep { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Logradouro { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [Required, MaxLength(2)]
    public string Uf { get; set; } = string.Empty;

    [Required, MaxLength(7)]
    public string MunicipioIbge { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Complemento { get; set; }

    [MaxLength(200)]
    public string? Referencia { get; set; }

    // Muda a cada edição para detectar formulários desatualizados.
    public Guid Versao { get; set; } = Guid.NewGuid();
}