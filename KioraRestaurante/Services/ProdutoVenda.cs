using KioraRestaurante.Models;

namespace KioraRestaurante.Services;

public static class ProdutoVenda
{
    public static bool EstaDisponivel(Produto produto)
    {
        if (!produto.Ativo || !produto.Disponivel || !produto.Categoria.Ativa)
        {
            return false;
        }

        if (!produto.EhCombo)
        {
            return true;
        }

        // Um combo sem composição não pode ser vendido.
        if (produto.Componentes.Count == 0)
        {
            return false;
        }

        return produto.Componentes.All(componente =>
            componente.Quantidade >= 1
            && componente.Quantidade <= 30
            && !componente.Produto.EhCombo
            && componente.Produto.Ativo
            && componente.Produto.Disponivel
            && componente.Produto.Categoria.Ativa);
    }

    public static string? DescreverComposicao(Produto produto)
    {
        if (!produto.EhCombo)
        {
            return null;
        }

        return string.Join(
            "; ",
            produto.Componentes
                .OrderBy(c => c.ProdutoId)
                .Select(c =>
                    $"{c.Quantidade} × {c.Produto.Nome}"));
    }
}