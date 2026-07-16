using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class HouseholdIngredientPreferenceService(BrowserAppStore store)
{
    public async Task<List<HouseholdIngredientPreferenceRow>> GetRowsAsync()
    {
        var state = await store.GetStateAsync();
        var preferences = state.HouseholdIngredientPreferences
            .Where(preference => !string.IsNullOrWhiteSpace(preference.IngredientKnowledgeId))
            .GroupBy(preference => preference.IngredientKnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return state.Ingredients
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.KnowledgeId))
            .OrderBy(ingredient => ingredient.Category)
            .ThenBy(ingredient => ingredient.Name)
            .Select(ingredient =>
            {
                var preference = preferences.TryGetValue(ingredient.KnowledgeId, out var existing)
                    ? ClonePreference(existing)
                    : new HouseholdIngredientPreference { IngredientKnowledgeId = ingredient.KnowledgeId };

                return new HouseholdIngredientPreferenceRow(CloneIngredientSummary(ingredient), preference);
            })
            .ToList();
    }

    public async Task SaveAsync(HouseholdIngredientPreference preference)
    {
        var normalized = Normalize(preference);
        if (string.IsNullOrWhiteSpace(normalized.IngredientKnowledgeId))
        {
            return;
        }

        var state = await store.GetStateAsync();
        state.HouseholdIngredientPreferences.RemoveAll(existing =>
            string.Equals(
                existing.IngredientKnowledgeId,
                normalized.IngredientKnowledgeId,
                StringComparison.OrdinalIgnoreCase));

        if (!IsDefault(normalized))
        {
            state.HouseholdIngredientPreferences.Add(normalized);
        }

        await store.SaveAsync();
    }

    private static HouseholdIngredientPreference Normalize(HouseholdIngredientPreference preference) => new()
    {
        IngredientKnowledgeId = preference.IngredientKnowledgeId.Trim(),
        IsFrequentlyUsed = preference.IsFrequentlyUsed,
        IsUsuallyAvailable = preference.IsUsuallyAvailable,
        UseFrequency = IngredientUseFrequency.All.Contains(preference.UseFrequency)
            ? preference.UseFrequency
            : IngredientUseFrequency.Occasional,
        PreferredAlias = preference.PreferredAlias.Trim(),
        HouseholdNotes = preference.HouseholdNotes.Trim()
    };

    private static bool IsDefault(HouseholdIngredientPreference preference) =>
        !preference.IsFrequentlyUsed
        && !preference.IsUsuallyAvailable
        && preference.UseFrequency == IngredientUseFrequency.Occasional
        && string.IsNullOrWhiteSpace(preference.PreferredAlias)
        && string.IsNullOrWhiteSpace(preference.HouseholdNotes);

    private static HouseholdIngredientPreference ClonePreference(HouseholdIngredientPreference preference) => new()
    {
        IngredientKnowledgeId = preference.IngredientKnowledgeId,
        IsFrequentlyUsed = preference.IsFrequentlyUsed,
        IsUsuallyAvailable = preference.IsUsuallyAvailable,
        UseFrequency = preference.UseFrequency,
        PreferredAlias = preference.PreferredAlias,
        HouseholdNotes = preference.HouseholdNotes
    };

    private static Ingredient CloneIngredientSummary(Ingredient ingredient) => new()
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
        NutritionState = ingredient.NutritionState,
        NutritionSource = ingredient.NutritionSource,
        NutritionSourceId = ingredient.NutritionSourceId,
        NutritionLastUpdated = ingredient.NutritionLastUpdated
    };
}

public sealed record HouseholdIngredientPreferenceRow(
    Ingredient Ingredient,
    HouseholdIngredientPreference Preference);
