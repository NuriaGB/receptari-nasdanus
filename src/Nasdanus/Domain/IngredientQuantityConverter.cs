namespace Nasdanus.Domain;

public static class IngredientQuantityConverter
{
    public static decimal? ToGrams(RecipeIngredient ingredient, decimal scale)
    {
        var quantity = IngredientScaling.ParseQuantity(ingredient.Quantity);
        if (quantity is null)
        {
            return null;
        }

        var scaledQuantity = IngredientScaling.ScaleQuantity(quantity.Value, ingredient.ScalingMode, scale);
        var unit = string.IsNullOrWhiteSpace(ingredient.Unit)
            ? ingredient.Ingredient?.DefaultUnit ?? string.Empty
            : ingredient.Unit;

        return UnitToGrams(scaledQuantity, unit, ingredient);
    }

    public static decimal? UnitToGrams(decimal quantity, string unit, RecipeIngredient ingredient)
    {
        if (CustomUnitWeightInGrams(ingredient, unit) is decimal customWeight)
        {
            return quantity * customWeight;
        }

        var normalized = FoodText.Normalize(unit);
        return normalized switch
        {
            "" => quantity,
            "g" => quantity,
            "gr" => quantity,
            "gram" => quantity,
            "grams" => quantity,
            "kg" => quantity * 1000m,
            "mg" => quantity / 1000m,
            "ml" => quantity,
            "cl" => quantity * 10m,
            "l" => quantity * 1000m,
            "cullerada" => quantity * 15m,
            "cullerades" => quantity * 15m,
            "tbsp" => quantity * 15m,
            "culleradeta" => quantity * 5m,
            "culleradetes" => quantity * 5m,
            "tsp" => quantity * 5m,
            "pessic" => quantity,
            "pessics" => quantity,
            "dent" => quantity * 5m,
            "dents" => quantity * 5m,
            "grapat" => quantity * 30m,
            "grapats" => quantity * 30m,
            "trosset" => quantity * 5m,
            "trossets" => quantity * 5m,
            "fulla" => quantity * 2m,
            "fulles" => quantity * 2m,
            "unit" => UnitWeightInGrams(ingredient) is decimal canonicalUnitWeight ? quantity * canonicalUnitWeight : null,
            "units" => UnitWeightInGrams(ingredient) is decimal canonicalUnitsWeight ? quantity * canonicalUnitsWeight : null,
            "unitat" => UnitWeightInGrams(ingredient) is decimal unitWeight ? quantity * unitWeight : null,
            "unitats" => UnitWeightInGrams(ingredient) is decimal unitsWeight ? quantity * unitsWeight : null,
            "u" => UnitWeightInGrams(ingredient) is decimal shortUnitWeight ? quantity * shortUnitWeight : null,
            _ => null
        };
    }

    private static decimal? CustomUnitWeightInGrams(RecipeIngredient ingredient, string unit)
    {
        var conversions = ingredient.Ingredient?.QuantityConversions;
        if (conversions is null || conversions.Count == 0)
        {
            return null;
        }

        var keys = ConversionLookupKeys(ingredient, unit)
            .Select(FoodText.Normalize)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return conversions
            .Where(conversion => conversion.Grams > 0)
            .FirstOrDefault(conversion => keys.Contains(FoodText.Normalize(conversion.Measure)))
            ?.Grams;
    }

    private static IEnumerable<string> ConversionLookupKeys(RecipeIngredient ingredient, string unit)
    {
        yield return unit;
        yield return ingredient.Unit;
        yield return ingredient.Name;
        yield return ingredient.DisplayName;
    }

    private static decimal? UnitWeightInGrams(RecipeIngredient ingredient) =>
        UnitWeightInGrams(ingredient.Name) ?? UnitWeightInGrams(ingredient.DisplayName);

    private static decimal? UnitWeightInGrams(string ingredientName)
    {
        var name = FoodText.Normalize(ingredientName);
        if (ContainsAny(name, "tomaquet cherry", "tomaquet xerri", "cherry", "xerri"))
        {
            return 15m;
        }

        if (ContainsAny(name, "zanui", "zanuy"))
        {
            return 62.5m;
        }

        if (ContainsAny(name, "durum"))
        {
            return 63m;
        }

        if (ContainsAny(name, "tortita", "tortilla", "wrap"))
        {
            return 63m;
        }

        if (ContainsAny(name, "ou", "egg"))
        {
            return 50m;
        }

        if (ContainsAny(name, "ceba", "onion"))
        {
            return 150m;
        }

        if (ContainsAny(name, "pastanaga", "carrot"))
        {
            return 60m;
        }

        if (ContainsAny(name, "carbasso", "zucchini"))
        {
            return 200m;
        }

        if (ContainsAny(name, "tomaquet", "tomato"))
        {
            return 120m;
        }

        if (ContainsAny(name, "patata", "potato"))
        {
            return 150m;
        }

        if (ContainsAny(name, "pebrot", "pepper"))
        {
            return 120m;
        }

        if (ContainsAny(name, "llimona", "lemon"))
        {
            return 60m;
        }

        if (ContainsAny(name, "poma", "apple"))
        {
            return 150m;
        }

        if (ContainsAny(name, "platan", "banana"))
        {
            return 120m;
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
