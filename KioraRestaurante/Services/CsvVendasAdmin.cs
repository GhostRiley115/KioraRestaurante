using System.Globalization;
using System.Text;
using KioraRestaurante.DTOs.Admin;
namespace KioraRestaurante.Services;

public static class CsvVendasAdmin
{
    // Nomes históricos também são entrada de usuário: neutraliza fórmulas ao abrir em planilhas.
    private static string Celula(string valor)
    {
        if (valor.TrimStart().StartsWith('=') || valor.TrimStart().StartsWith('+') || valor.TrimStart().StartsWith('-') || valor.TrimStart().StartsWith('@')) valor="'"+valor;
        return "\""+valor.Replace("\"","\"\"")+"\"";
    }
    public static byte[] Gerar(IEnumerable<VendaAdminDTO> vendas)
    {
        var cultura=CultureInfo.GetCultureInfo("pt-BR");
        var csv=new StringBuilder("Canal;Identificação;Data em Brasília;Total;Frete;Cupom ou desconto da comanda\r\n");
        foreach(var v in vendas)
            csv.AppendLine(string.Join(";",new[] {v.Canal=="delivery"?"Delivery":"Salão",v.Identificacao,
                HorarioRestaurante.ParaLocal(v.DataUtc).ToString("dd/MM/yyyy HH:mm",cultura),v.Total.ToString("F2",cultura),v.Frete.ToString("F2",cultura),v.DescontoPedido.ToString("F2",cultura)}.Select(Celula)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
