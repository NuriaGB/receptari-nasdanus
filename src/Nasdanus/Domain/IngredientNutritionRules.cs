namespace Nasdanus.Domain;

public static class IngredientNutritionRules
{
    public static bool ShouldIgnoreForNutrition(Ingredient? ingredient) =>
        IsSpice(ingredient) && !IsSalt(ingredient);

    public static bool RequiresNutritionData(Ingredient? ingredient) =>
        ingredient is not null
        && !ShouldIgnoreForNutrition(ingredient)
        && !IsSalt(ingredient);

    public static bool HasUsableNutritionForCalculation(Ingredient? ingredient) =>
        ingredient is not null
        && (ShouldIgnoreForNutrition(ingredient)
            || IsSalt(ingredient)
            || ingredient.NutritionPer100Grams?.HasAnyValue == true);

    public static bool IsSalt(Ingredient? ingredient)
    {
        if (ingredient is null)
        {
            return false;
        }

        return IngredientNames(ingredient)
            .Select(FoodText.Normalize)
            .Any(IsSaltName);
    }

    private static bool IsSpice(Ingredient? ingredient) =>
        ingredient is not null
        && string.Equals(ingredient.Category, IngredientCategory.Spices, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> IngredientNames(Ingredient ingredient) =>
        new[] { ingredient.Name, ingredient.CatalanName, ingredient.SpanishName }
            .Concat(ingredient.Aliases);

    private static bool IsSaltName(string name) =>
        name == "sal"
        || name == "salt"
        || name.StartsWith("sal ", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("salt ", StringComparison.OrdinalIgnoreCase);
}
