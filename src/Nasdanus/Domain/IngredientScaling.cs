using System.Globalization;

namespace Nasdanus.Domain;

public static class IngredientScaling
{
    public static decimal ScaleFactor(int defaultServings, int plannedServings)
    {
        if (defaultServings <= 0 || plannedServings <= 0)
        {
            return 1;
        }

        return plannedServings / (decimal)defaultServings;
    }

    public static string FormatIngredient(RecipeIngredient ingredient, decimal scale)
    {
        var quantity = ScaledQuantityText(ingredient.Quantity, ingredient.ScalingMode, scale);
        var amount = string.Join(" ", new[] { quantity, ingredient.Unit }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var suffix = ingredient.ScalingMode switch
        {
            IngredientScalingMode.Approximate when !string.IsNullOrWhiteSpace(amount) => "aprox. ",
            IngredientScalingMode.ToTaste when string.IsNullOrWhiteSpace(amount) => "al gust ",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(amount)
            ? $"{suffix}{ingredient.DisplayName}".Trim()
            : $"{suffix}{amount} {ingredient.DisplayName}".Trim();
    }

    public static string FormatStepIngredient(RecipeStepIngredientReference reference, decimal scale)
    {
        var mode = reference.Ingredient?.ScalingMode ?? IngredientScalingMode.Linear;
        var name = reference.Ingredient?.DisplayName ?? reference.IngredientName;
        var quantity = reference.Quantity is null
            ? ScaledQuantityText(reference.QuantityText, mode, scale)
            : ScaledQuantityText(reference.Quantity.Value, mode, scale);
        var amount = string.Join(" ", new[] { quantity, reference.Unit }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var suffix = mode switch
        {
            IngredientScalingMode.Approximate when !string.IsNullOrWhiteSpace(amount) => "aprox. ",
            IngredientScalingMode.ToTaste when string.IsNullOrWhiteSpace(amount) => "al gust ",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(amount)
            ? $"{suffix}{name}".Trim()
            : $"{suffix}{amount} {name}".Trim();
    }

    public static decimal? ParseQuantity(string quantity)
    {
        var normalized = quantity.Trim().Replace(',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var fractionParts = normalized.Split('/', StringSplitOptions.TrimEntries);
        if (fractionParts.Length == 2
            && decimal.TryParse(fractionParts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
            && decimal.TryParse(fractionParts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            return numerator / denominator;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string ScaledQuantityText(string quantity, string scalingMode, decimal scale)
    {
        var parsedQuantity = ParseQuantity(quantity);
        return parsedQuantity is null
            ? quantity
            : ScaledQuantityText(parsedQuantity.Value, scalingMode, scale);
    }

    private static string ScaledQuantityText(decimal quantity, string scalingMode, decimal scale)
    {
        return FormatQuantity(ScaleQuantity(quantity, scalingMode, scale));
    }

    public static decimal ScaleQuantity(decimal quantity, string scalingMode, decimal scale)
    {
        var effectiveScale = scalingMode is IngredientScalingMode.Fixed or IngredientScalingMode.ToTaste
            ? 1
            : scale;

        return quantity * effectiveScale;
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.##", CultureInfo.InvariantCulture);
}
