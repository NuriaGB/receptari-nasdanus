using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class IngredientKnowledgeService(BrowserAppStore store)
{
    public async Task<List<Ingredient>> GetKnownIngredientsAsync()
    {
        var state = await store.GetStateAsync();
        return state.Ingredients
            .Where(IsKnownIngredient)
            .OrderBy(ingredient => ingredient.Name)
            .Select(Clone)
            .ToList();
    }

    private static bool IsKnownIngredient(Ingredient ingredient) =>
        !string.IsNullOrWhiteSpace(ingredient.KnowledgeId);

    private static Ingredient Clone(Ingredient ingredient) => new()
    {
        Id = ingredient.Id,
        KnowledgeId = ingredient.KnowledgeId,
        Name = ingredient.Name,
        Aliases = ingredient.Aliases.ToList(),
        Category = ingredient.Category,
        DefaultUnit = ingredient.DefaultUnit,
        PantryCategory = ingredient.PantryCategory,
        CanFreeze = ingredient.CanFreeze,
        Seasonality = ingredient.Seasonality,
        NutritionPer100Grams = ingredient.NutritionPer100Grams is null
            ? null
            : new IngredientNutrition
            {
                CaloriesKcal = ingredient.NutritionPer100Grams.CaloriesKcal,
                ProteinGrams = ingredient.NutritionPer100Grams.ProteinGrams,
                CarbohydrateGrams = ingredient.NutritionPer100Grams.CarbohydrateGrams,
                FatGrams = ingredient.NutritionPer100Grams.FatGrams,
                FibreGrams = ingredient.NutritionPer100Grams.FibreGrams,
                SugarGrams = ingredient.NutritionPer100Grams.SugarGrams,
                SaltGrams = ingredient.NutritionPer100Grams.SaltGrams
            },
        NutritionSource = ingredient.NutritionSource
    };
}
