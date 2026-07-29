using System.Text;
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

    public async Task<IngredientKnowledgeManualSaveResult?> CreateManualIngredientAsync(IngredientKnowledgeManualEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Name))
        {
            return null;
        }

        var state = await store.GetStateAsync();
        var existing = FindExistingManualMatch(state.Ingredients, edit);
        if (existing is not null)
        {
            ApplyManualEdit(existing, edit, preserveExistingName: true, mergeAliases: true);
            await store.SaveAsync();
            return new IngredientKnowledgeManualSaveResult(Clone(existing), Created: false);
        }

        var ingredient = new Ingredient
        {
            Id = store.NextId(state),
            KnowledgeId = UniqueManualKnowledgeId(state.Ingredients, edit.Name)
        };

        ApplyManualEdit(ingredient, edit, preserveExistingName: false, mergeAliases: false);
        state.Ingredients.Add(ingredient);
        await store.SaveAsync();
        return new IngredientKnowledgeManualSaveResult(Clone(ingredient), Created: true);
    }

    public async Task<bool> SaveIngredientAsync(string knowledgeId, IngredientKnowledgeManualEdit edit)
    {
        if (string.IsNullOrWhiteSpace(knowledgeId) || string.IsNullOrWhiteSpace(edit.Name))
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

        ApplyManualEdit(ingredient, edit, preserveExistingName: false, mergeAliases: false);
        await store.SaveAsync();
        return true;
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

    private static void ApplyManualEdit(
        Ingredient ingredient,
        IngredientKnowledgeManualEdit edit,
        bool preserveExistingName,
        bool mergeAliases)
    {
        if (!preserveExistingName)
        {
            ingredient.Name = edit.Name.Trim();
            ingredient.CatalanName = edit.CatalanName.Trim();
            ingredient.SpanishName = edit.SpanishName.Trim();
        }
        else
        {
            ingredient.CatalanName = string.IsNullOrWhiteSpace(edit.CatalanName)
                ? ingredient.CatalanName
                : edit.CatalanName.Trim();
            ingredient.SpanishName = string.IsNullOrWhiteSpace(edit.SpanishName)
                ? ingredient.SpanishName
                : edit.SpanishName.Trim();
        }

        var aliases = mergeAliases
            ? ingredient.Aliases.Concat(edit.Aliases).Append(edit.Name)
            : edit.Aliases;

        ingredient.Aliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Where(alias => !string.Equals(alias, ingredient.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias)
            .ToList();
        if (preserveExistingName)
        {
            if (!string.Equals(edit.Category, IngredientCategory.Other, StringComparison.OrdinalIgnoreCase))
            {
                ingredient.Category = NormalizeIngredientCategory(edit.Category);
            }

            if (!string.IsNullOrWhiteSpace(edit.Subcategory))
            {
                ingredient.Subcategory = edit.Subcategory.Trim();
            }

            if (!string.IsNullOrWhiteSpace(edit.DefaultUnit)
                && !string.Equals(edit.DefaultUnit, "g", StringComparison.OrdinalIgnoreCase))
            {
                ingredient.DefaultUnit = edit.DefaultUnit.Trim();
            }

            if (!string.Equals(edit.PantryCategory, ShoppingCategory.Other, StringComparison.OrdinalIgnoreCase))
            {
                ingredient.PantryCategory = NormalizeShoppingCategory(edit.PantryCategory);
            }

            ingredient.CanFreeze = ingredient.CanFreeze || edit.CanFreeze;
        }
        else
        {
            ingredient.Category = NormalizeIngredientCategory(edit.Category);
            ingredient.Subcategory = edit.Subcategory.Trim();
            ingredient.DefaultUnit = string.IsNullOrWhiteSpace(edit.DefaultUnit) ? "g" : edit.DefaultUnit.Trim();
            ingredient.PantryCategory = NormalizeShoppingCategory(edit.PantryCategory);
            ingredient.CanFreeze = edit.CanFreeze;
        }

        if (edit.Nutrition?.HasAnyValue == true)
        {
            ingredient.NutritionPer100Grams = CloneNutrition(edit.Nutrition);
            ingredient.NutritionSource = string.IsNullOrWhiteSpace(edit.NutritionSource)
                ? "manual"
                : edit.NutritionSource.Trim();
            ingredient.NutritionSourceId = string.IsNullOrWhiteSpace(edit.NutritionSourceId)
                ? ingredient.Name
                : edit.NutritionSourceId.Trim();
            ingredient.NutritionLastUpdated = DateTimeOffset.UtcNow;
        }
        else if (!preserveExistingName)
        {
            ingredient.NutritionPer100Grams = null;
            ingredient.NutritionSource = string.Empty;
            ingredient.NutritionSourceId = string.Empty;
            ingredient.NutritionLastUpdated = null;
        }
    }

    private static Ingredient? FindExistingManualMatch(IEnumerable<Ingredient> ingredients, IngredientKnowledgeManualEdit edit)
    {
        var requestedKeys = IngredientKeys(edit)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requestedKeys.Count == 0)
        {
            return null;
        }

        return ingredients
            .Where(IsKnownIngredient)
            .FirstOrDefault(ingredient => IngredientKeys(ingredient).Any(requestedKeys.Contains));
    }

    private static IEnumerable<string> IngredientKeys(IngredientKnowledgeManualEdit edit)
    {
        yield return FoodText.Normalize(edit.Name);
        yield return FoodText.Normalize(edit.CatalanName);
        yield return FoodText.Normalize(edit.SpanishName);
        foreach (var alias in edit.Aliases)
        {
            yield return FoodText.Normalize(alias);
        }
    }

    private static IEnumerable<string> IngredientKeys(Ingredient ingredient)
    {
        yield return FoodText.Normalize(ingredient.Name);
        yield return FoodText.Normalize(ingredient.CatalanName);
        yield return FoodText.Normalize(ingredient.SpanishName);
        foreach (var alias in ingredient.Aliases)
        {
            yield return FoodText.Normalize(alias);
        }
    }

    private static string UniqueManualKnowledgeId(IEnumerable<Ingredient> ingredients, string name)
    {
        var slug = SlugFor(name);
        var baseId = $"manual:{(string.IsNullOrWhiteSpace(slug) ? "ingredient" : slug)}";
        var id = baseId;
        var index = 2;

        while (ingredients.Any(ingredient => string.Equals(ingredient.KnowledgeId, id, StringComparison.OrdinalIgnoreCase)))
        {
            id = $"{baseId}-{index}";
            index++;
        }

        return id;
    }

    private static string SlugFor(string value)
    {
        var normalized = FoodText.Normalize(value);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string NormalizeIngredientCategory(string category) =>
        IngredientCategory.All.Contains(category) ? category : IngredientCategory.Other;

    private static string NormalizeShoppingCategory(string category) =>
        ShoppingCategory.DisplayOrder.Contains(category) ? category : ShoppingCategory.Other;

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
