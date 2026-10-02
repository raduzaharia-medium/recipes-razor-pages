using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Recipes.Infrastructure;

/// <summary>Binds decimals typed with either a comma or a dot ("1,5" or "1.5"); an empty value is zero.</summary>
public class DecimalCommaBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(decimal) ? new DecimalCommaBinder(context.Metadata.IsNullableValueType) : null;

    private class DecimalCommaBinder(bool nullable) : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext context)
        {
            var value = context.ValueProvider.GetValue(context.ModelName);
            if (value == ValueProviderResult.None) return Task.CompletedTask;

            context.ModelState.SetModelValue(context.ModelName, value);
            var text = value.FirstValue?.Trim().Replace(',', '.');

            if (string.IsNullOrEmpty(text)) context.Result = ModelBindingResult.Success(nullable ? null : 0m);
            else if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) context.Result = ModelBindingResult.Success(number);
            else context.ModelState.TryAddModelError(context.ModelName, $"'{value.FirstValue}' is not a valid number.");

            return Task.CompletedTask;
        }
    }
}
