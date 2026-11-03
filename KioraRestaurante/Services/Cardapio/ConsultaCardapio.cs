using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KioraRestaurante.DTOs.Categoria;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.ViewModels;

namespace KioraRestaurante.Services.Cardapio;

public static class ConsultaCardapio
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly StringComparer Comparador = StringComparer.Create(Cultura, true);

    public static int OrdemCategoria(string nome) => Normalizar(nome) switch
    {
        "combo" or "combos" => 0,
        "prato principal" or "pratos principais" => 1,
        "entrada" or "entradas" => 2,
        "sobremesa" or "sobremesas" => 3,
        "bebida" or "bebidas" => 4,
        _ => 5 // Categorias novas aparecem depois das conhecidas.
    };

    public static CardapioViewModel Montar(
        List<ProdutoResponseDTO> produtos,
        List<CategoriaResponseDTO> categorias,
        int? categoriaId,
        FiltroCardapioViewModel filtros)
    {
        filtros.Busca = filtros.Busca?.Trim();
        filtros.PrecoMinimo = filtros.PrecoMinimo?.Trim();
        filtros.PrecoMaximo = filtros.PrecoMaximo?.Trim();
        filtros.Ordenacao = string.IsNullOrWhiteSpace(filtros.Ordenacao)
            ? "padrao" : filtros.Ordenacao.Trim().ToLowerInvariant();

        var model = new CardapioViewModel
        {
            Categorias = categorias.OrderBy(c => OrdemCategoria(c.Nome))
                .ThenBy(c => c.Nome, Comparador).ToList(),
            CategoriaSelecionadaId = categoriaId,
            Titulo = categorias.FirstOrDefault(c => c.Id == categoriaId)?.Nome
                ?? "Todos os produtos",
            Filtros = filtros,
            ExibirSeparadores = categoriaId == null && filtros.Ordenacao == "padrao"
        };

        if (filtros.Busca?.Length > 100)
            model.ErrosFiltros.Add("A busca deve ter no máximo 100 caracteres.");

        var minimo = LerPreco(filtros.PrecoMinimo, "mínimo", model.ErrosFiltros);
        var maximo = LerPreco(filtros.PrecoMaximo, "máximo", model.ErrosFiltros);
        if (minimo.HasValue && maximo.HasValue && minimo > maximo)
            model.ErrosFiltros.Add("O preço mínimo não pode ser maior que o máximo.");

        if (filtros.Ordenacao is not ("padrao" or "menor-preco" or "maior-preco" or "nome-az" or "nome-za"))
            model.ErrosFiltros.Add("Selecione uma ordenação válida.");

        // Não exibe um resultado parcial que ignore silenciosamente um filtro inválido.
        if (model.ErrosFiltros.Count > 0) return model;

        IEnumerable<ProdutoResponseDTO> consulta = produtos;
        if (categoriaId.HasValue)
            consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);
        if (!string.IsNullOrEmpty(filtros.Busca))
            consulta = consulta.Where(p => Cultura.CompareInfo.IndexOf(
                p.Nome, filtros.Busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

        // Preco já é o valor final calculado pelo ProdutoService, com desconto.
        if (minimo.HasValue) consulta = consulta.Where(p => p.Preco >= minimo.Value);
        if (maximo.HasValue) consulta = consulta.Where(p => p.Preco <= maximo.Value);
        if (filtros.SomenteDisponiveis) consulta = consulta.Where(p => p.Disponivel);
        if (filtros.SomentePromocoes) consulta = consulta.Where(p => p.DescontoPercentual > 0);

        var ordenados = filtros.Ordenacao switch
        {
            "menor-preco" => consulta.OrderBy(p => p.Preco).ThenBy(p => p.Nome, Comparador),
            "maior-preco" => consulta.OrderByDescending(p => p.Preco).ThenBy(p => p.Nome, Comparador),
            "nome-za" => consulta.OrderByDescending(p => p.Nome, Comparador),
            _ => consulta.OrderBy(p => p.Nome, Comparador)
        };
        model.Produtos = ordenados.ThenBy(p => p.Id).ToList();

        if (model.ExibirSeparadores)
        {
            // Combo é uma seção visual: não mudamos CategoriaId nem criamos tabelas.
            model.Secoes = model.Produtos
                .GroupBy(p => EhSecaoCombos(p) ? "combos" : "categoria-" + p.CategoriaId)
                .Select(g => new SecaoCardapioViewModel
                {
                    Id = g.Key,
                    Titulo = EhSecaoCombos(g.First()) ? "Combos" : g.First().NomeCategoria,
                    Produtos = g.ToList()
                })
                .OrderBy(s => OrdemCategoria(s.Titulo))
                .ThenBy(s => s.Titulo, Comparador).ThenBy(s => s.Id).ToList();
        }
        else if (model.Produtos.Count > 0)
        {
            // Ordenações explícitas comparam todos os resultados da seleção atual.
            model.Secoes.Add(new SecaoCardapioViewModel
            {
                Id = "resultados", Titulo = model.Titulo, Produtos = model.Produtos
            });
        }
        return model;
    }

    // O tipo do produto define se ele é combo.
    // Alterar a ordem das categorias não muda essa identificação.
    private static bool EhSecaoCombos(ProdutoResponseDTO produto) =>
        produto.EhCombo;

    private static decimal? LerPreco(string? texto, string limite, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        // Sem R$, separador de milhar, sinal negativo ou mais de duas casas decimais.
        if (texto.Length > 11
            || !Regex.IsMatch(texto, @"\A[0-9]{1,8}(?:[.,][0-9]{1,2})?\z")
            || !decimal.TryParse(texto.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var valor))
        {
            erros.Add($"Informe um preço {limite} válido, como 25,90, sem R$ ou separador de milhar.");
            return null;
        }
        return valor;
    }

    private static string Normalizar(string texto)
    {
        var caracteres = texto.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(caracteres.ToArray()).Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}