using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KioraRestaurante.ModelBinding;

// Campos monetários dos cupons aceitam vírgula ou ponto decimal, sem separador de milhar.
public class ValorCupomModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var valor = context.ValueProvider.GetValue(context.ModelName);
        if (valor == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, valor);
        var texto = valor.FirstValue?.Trim() ?? "";
        if (texto.Length == 0 && Nullable.GetUnderlyingType(context.ModelType) == typeof(decimal))
            context.Result = ModelBindingResult.Success(null);
        else if (Regex.IsMatch(texto, @"^\d{1,8}([.,]\d{1,2})?$")
            && decimal.TryParse(texto.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero))
            context.Result = ModelBindingResult.Success(numero);
        else context.ModelState.TryAddModelError(context.ModelName, "Informe um valor positivo com até duas casas decimais, como 10,50.");
        return Task.CompletedTask;
    }
}
