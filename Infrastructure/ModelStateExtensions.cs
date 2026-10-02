using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Recipes.Infrastructure;

public static class ModelStateExtensions
{
    extension(ModelStateDictionary state)
    {
        /// <summary>Adds the error when the value is empty or only spaces.</summary>
        public void Required(string? value, string field, string message)
        {
            if (string.IsNullOrWhiteSpace(value)) state.AddModelError(field, message);
        }

        /// <summary>Adds the error when the condition holds.</summary>
        public void ErrorIf(bool condition, string field, string message)
        {
            if (condition) state.AddModelError(field, message);
        }
    }
}
