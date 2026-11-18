using System.Globalization;
using KioraRestaurante.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace KioraRestaurante.Tests;

public class ValoresCupomTests
{
    [Theory] [InlineData("10,50", true)] [InlineData("10.50", true)] [InlineData("10,500", false)] [InlineData("1.000,50", false)] [InlineData("-10", false)]
    public async Task SeparadorDecimalNaoDependeDoIdiomaDoServidor(string texto, bool aceito)
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal));
        var context = DefaultModelBindingContext.CreateBindingContext(new ActionContext { HttpContext = new DefaultHttpContext() },
            new FormValueProvider(BindingSource.Form, new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["Valor"] = texto }), CultureInfo.GetCultureInfo("en-US")),
            metadata, null, "Valor");
        await new ValorCupomModelBinder().BindModelAsync(context);
        Assert.Equal(aceito, context.Result.IsModelSet);
        if (aceito) Assert.Equal(10.50m, context.Result.Model);
        else Assert.False(context.ModelState.IsValid);
    }
}
