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

    public async Task<bool> SaveNutritionAsync(string knowledgeId, IngredientNutritionManualEdit edit)
    {
        if (string.IsNullOrWhiteSpace(knowledgeId))
        {
            return false;
        }

        var state = await store.GetStateAsync();
        var ingredient = state.Ingredients.FirstOrDefault(ingredient =>
            string.Equals(ingredient.KnowledgeId, knowledgeId, StringComparison.OrdinalIgnoreCase));
        if (ingredient is null)
        {
            return false;
        }

        ingredient.NutritionPer100Grams = CloneNutrition(edit.Nutrition);
        ingredient.NutritionSource = string.IsNullOrWhiteSpace(edit.Source)
            ? "manual"
            : edit.Source.Trim();
        ingredient.NutritionSourceId = string.IsNullOrWhiteSpace(edit.SourceId)
            ? "manual"
            : edit.SourceId.Trim();
        ingredient.NutritionLastUpdated = DateTimeOffset.UtcNow;

        await store.SaveAsync();
        return true;
    }

    private static bool IsKnownIngredient(Ingredient ingredient) =>
        !string.IsNullOrWhiteSpace(ingredient.KnowledgeId);

    private static Ingredient Clone(Ingredient ingredient) => new()
    {
        Id = ingredient.Id,
        KnowledgeId = ingredient.KnowledgeId,
        Name = ingredient.Name,
        CatalanName = ingredient.CatalanName,
        SpanishName = ingredient.SpanishName,
        Aliases = ingredient.Aliases.ToList(),
        Category = ingredient.Category,
        Subcategory = ingredient.Subcategory,
        DefaultUnit = ingredient.DefaultUnit,
        PantryCategory = ingredient.PantryCategory,
        CanFreeze = ingredient.CanFreeze,
        Seasonality = ingredient.Seasonality,
        NutritionPer100Grams = ingredient.NutritionPer100Grams is null ? null : CloneNutrition(ingredient.NutritionPer100Grams),
        NutritionState = ingredient.NutritionState,
        NutritionSource = ingredient.NutritionSource,
        NutritionSourceId = ingredient.NutritionSourceId,
        NutritionLastUpdated = ingredient.NutritionLastUpdated
    };

    private static IngredientNutrition CloneNutrition(IngredientNutrition nutrition) => new()
    {
        CaloriesKcal = nutrition.CaloriesKcal,
        ProteinGrams = nutrition.ProteinGrams,
        CarbohydrateGrams = nutrition.CarbohydrateGrams,
        FatGrams = nutrition.FatGrams,
        FibreGrams = nutrition.FibreGrams,
        SugarGrams = nutrition.SugarGrams,
        SaltGrams = nutrition.SaltGrams
    };
}
