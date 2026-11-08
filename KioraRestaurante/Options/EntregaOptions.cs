namespace KioraRestaurante.Options;

public class EntregaOptions
{
    public EnderecoRestauranteOptions Restaurante { get; set; } = new();
    public int LimiteMetros { get; set; } = 35000;
    public FaixaEntrega[] Faixas { get; set; } =
    [
        new() { AteMetros = 5000, Valor = 5m },
        new() { AteMetros = 10000, Valor = 8m },
        new() { AteMetros = 15000, Valor = 12m },
        new() { AteMetros = 20000, Valor = 16m },
        new() { AteMetros = 25000, Valor = 20m },
        new() { AteMetros = 30000, Valor = 24m },
        new() { AteMetros = 35000, Valor = 28m }
    ];
    public RotasEntregaOptions Rotas { get; set; } = new();
    public GeocodificacaoEntregaOptions Geocodificacao { get; set; } = new();
}

public class FaixaEntrega
{
    public int AteMetros { get; set; }
    public decimal Valor { get; set; }
}

public class RotasEntregaOptions
{
    public string ApiKey { get; set; } = "";
}

public class GeocodificacaoEntregaOptions
{
    public string ApiKey { get; set; } = "";
}

public class EnderecoRestauranteOptions
{
    public string Logradouro { get; set; } = "Rua dos Pinheiros";
    public string Numero { get; set; } = "1234";
    public string Bairro { get; set; } = "Pinheiros";
    public string Cidade { get; set; } = "São Paulo";
    public string Uf { get; set; } = "SP";
    public string EnderecoCompleto => $"{Logradouro}, {Numero}, {Bairro}, {Cidade}, {Uf}, Brasil";
}
