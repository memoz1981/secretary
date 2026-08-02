using Microsoft.AspNetCore.Mvc.ModelBinding;
using NodaTime;
using NodaTime.Text;

namespace Secretary.Api.ModelBinding;

/// <summary>Lets query/route parameters bind directly to a NodaTime Instant (ISO 8601,
/// e.g. `?from=2026-07-11T00:00:00Z`) — mirrors StronglyTypedIdModelBinderProvider.</summary>
public sealed class InstantModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
        => context.Metadata.ModelType == typeof(Instant) || context.Metadata.ModelType == typeof(Instant?)
            ? new InstantModelBinder()
            : null;

    private sealed class InstantModelBinder : IModelBinder
    {
        private static readonly InstantPattern Pattern = InstantPattern.ExtendedIso;

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);
            var value = valueProviderResult.FirstValue;

            if (string.IsNullOrEmpty(value))
            {
                return Task.CompletedTask;
            }

            var result = Pattern.Parse(value);
            if (!result.Success)
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"'{value}' is not a valid ISO 8601 instant.");
                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(result.Value);
            return Task.CompletedTask;
        }
    }
}
