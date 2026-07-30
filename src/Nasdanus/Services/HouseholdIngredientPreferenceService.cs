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

    public async Task SaveManyAsync(IEnumerable<HouseholdIngredientPreference> preferences)
    {
        var normalizedPreferences = preferences
            .Select(Normalize)
            .Where(preference => !string.IsNullOrWhiteSpace(preference.IngredientKnowledgeId))
            .GroupBy(preference => preference.IngredientKnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        if (normalizedPreferences.Count == 0)
        {
            return;
        }

        var state = await store.GetStateAsync();
        var knowledgeIds = normalizedPreferences
            .Select(preference => preference.IngredientKnowledgeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        state.HouseholdIngredientPreferences.RemoveAll(existing =>
            knowledgeIds.Contains(existing.IngredientKnowledgeId));

        state.HouseholdIngredientPreferences.AddRange(
            normalizedPreferences.Where(preference => !IsDefault(preference)));

        await store.SaveAsync();
    }

    private static HouseholdIngredientPreference Normalize(HouseholdIngredientPreference preference) => new()
    {
        IngredientKnowledgeId = preference.IngredientKnowledgeId.Trim(),
        IsFavourite = preference.IsFavourite,
        IsFrequentlyUsed = preference.IsFrequentlyUsed,
        IsUsuallyAvailable = preference.IsUsuallyAvailable,
        IsAlwaysInPantry = preference.IsAlwaysInPantry,
        IsNormallyFrozen = preference.IsNormallyFrozen,
        UseFrequency = IngredientUseFrequency.All.Contains(preference.UseFrequency)
            ? preference.UseFrequency
            : IngredientUseFrequency.Occasional,
        PreferredAlias = preference.PreferredAlias.Trim(),
        HouseholdNotes = preference.HouseholdNotes.Trim()
    };

    private static bool IsDefault(HouseholdIngredientPreference preference) =>
        !preference.IsFavourite
        && !preference.IsFrequentlyUsed
        && !preference.IsUsuallyAvailable
        && !preference.IsAlwaysInPantry
        && !preference.IsNormallyFrozen
        && preference.UseFrequency == IngredientUseFrequency.Occasional
        && string.IsNullOrWhiteSpace(preference.PreferredAlias)
        && string.IsNullOrWhiteSpace(preference.HouseholdNotes);

    private static HouseholdIngredientPreference ClonePreference(HouseholdIngredientPreference preference) => new()
    {
        IngredientKnowledgeId = preference.IngredientKnowledgeId,
        IsFavourite = preference.IsFavourite,
        IsFrequentlyUsed = preference.IsFrequentlyUsed,
        IsUsuallyAvailable = preference.IsUsuallyAvailable,
        IsAlwaysInPantry = preference.IsAlwaysInPantry,
        IsNormallyFrozen = preference.IsNormallyFrozen,
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
        CanFreeze = ingredient.CanFreeze,
        Seasonality = ingredient.Seasonality,
        QuantityConversions = ingredient.QuantityConversions
            .Select(conversion => new IngredientQuantityConversion
            {
                Measure = conversion.Measure,
                Grams = conversion.Grams,
                Notes = conversion.Notes
            })
            .ToList(),
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
        NutritionState = ingredient.NutritionState,
        NutritionSource = ingredient.NutritionSource,
        NutritionSourceId = ingredient.NutritionSourceId,
        NutritionLastUpdated = ingredient.NutritionLastUpdated
    };
}

public sealed record HouseholdIngredientPreferenceRow(
    Ingredient Ingredient,
    HouseholdIngredientPreference Preference);
