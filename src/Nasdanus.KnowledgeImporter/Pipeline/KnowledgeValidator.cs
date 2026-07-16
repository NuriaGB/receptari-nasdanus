using Nasdanus.KnowledgeImporter.Domain;
using System.Text.Json;

namespace Nasdanus.KnowledgeImporter.Pipeline;

public sealed class KnowledgeValidator
{
    public KnowledgeValidationReport Validate(KnowledgeCatalog catalog)
    {
        var report = new KnowledgeValidationReport();
        ValidateCategories(catalog, report);
        ValidateUnits(catalog, report);
        ValidateAliases(catalog, report);
        ValidateIngredients(catalog, report);
        ValidateNutrition(catalog, report);
        ValidateTranslations(catalog, report);
        ValidateSources(catalog, report);
        ValidateSeedRecipeResolution(catalog, report);
        return report;
    }

    private static void ValidateCategories(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        var unknownCategories = catalog.Ingredients
            .Select(ingredient => ingredient.Category)
            .Concat(catalog.Ingredients.Select(ingredient => ingredient.PantryCategory))
            .Where(category => !KnowledgeCategories.All.Contains(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        report.UnknownCategories.AddRange(unknownCategories);
    }

    private static void ValidateUnits(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        report.MissingUnits.AddRange(catalog.Ingredients
            .Where(ingredient => string.IsNullOrWhiteSpace(ingredient.DefaultUnit) || !KnowledgeUnits.All.Contains(ingredient.DefaultUnit))
            .Select(ingredient => $"{ingredient.Id}: {ingredient.Name}")
            .OrderBy(value => value));
    }

    private static void ValidateAliases(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        var aliases = catalog.Ingredients
            .SelectMany(ingredient => ingredient.Aliases.Select(alias => new
            {
                Ingredient = ingredient,
                Alias = alias,
                Key = KnowledgeNormalizer.NormalizeKey(alias)
            }))
            .Where(value => !string.IsNullOrWhiteSpace(value.Key))
            .ToList();

        report.DuplicateAliases.AddRange(aliases
            .GroupBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(value => value.Ingredient.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(value => value.Ingredient.Name).Distinct())}")
            .OrderBy(value => value));
    }

    private static void ValidateIngredients(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        report.DuplicateIngredients.AddRange(catalog.Ingredients
            .GroupBy(ingredient => KnowledgeNormalizer.NormalizeKey(ingredient.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(ingredient => ingredient.Id))}")
            .OrderBy(value => value));
    }

    private static void ValidateNutrition(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        report.MissingNutrition.AddRange(catalog.Ingredients
            .Where(ingredient => ingredient.Nutrition?.HasCoreMacros != true)
            .Select(ingredient => $"{ingredient.Id}: {ingredient.Name}")
            .OrderBy(value => value));
    }

    private static void ValidateTranslations(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        report.MissingTranslations.AddRange(catalog.Ingredients
            .Where(ingredient => string.IsNullOrWhiteSpace(ingredient.CatalanName)
                || string.IsNullOrWhiteSpace(ingredient.SpanishName))
            .Select(ingredient => $"{ingredient.Id}: {ingredient.Name}")
            .OrderBy(value => value));
    }

    private static void ValidateSources(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        report.MissingSourceInformation.AddRange(catalog.Ingredients
            .Where(ingredient => string.IsNullOrWhiteSpace(ingredient.Source)
                || string.IsNullOrWhiteSpace(ingredient.SourceId))
            .Select(ingredient => $"{ingredient.Id}: {ingredient.Name}")
            .OrderBy(value => value));
    }

    private static void ValidateSeedRecipeResolution(KnowledgeCatalog catalog, KnowledgeValidationReport report)
    {
        var seedPath = Path.Combine("src", "Nasdanus", "wwwroot", "data", "nasdanus-seed.json");
        if (!File.Exists(seedPath))
        {
            return;
        }

        var knownKeys = catalog.Ingredients
            .SelectMany(IngredientKeysFor)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(File.ReadAllText(seedPath));
        if (!document.RootElement.TryGetProperty("Recipes", out var recipes)
            || recipes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var recipe in recipes.EnumerateArray())
        {
            var recipeName = recipe.TryGetProperty("Name", out var recipeNameProperty)
                ? recipeNameProperty.GetString() ?? "Recipe"
                : "Recipe";
            if (!recipe.TryGetProperty("Ingredients", out var ingredients)
                || ingredients.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var ingredient in ingredients.EnumerateArray())
            {
                var ingredientName = ingredient.TryGetProperty("Name", out var ingredientNameProperty)
                    ? ingredientNameProperty.GetString() ?? string.Empty
                    : string.Empty;
                var normalized = KnowledgeNormalizer.NormalizeKey(ingredientName);
                if (string.IsNullOrWhiteSpace(normalized)
                    || knownKeys.Any(key => IsIngredientNameMatch(normalized, key)))
                {
                    continue;
                }

                report.RecipesUsingUnresolvedIngredients.Add($"{recipeName}: {ingredientName}");
            }
        }

        report.RecipesUsingUnresolvedIngredients = report.RecipesUsingUnresolvedIngredients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToList();
    }

    private static bool IsIngredientNameMatch(string normalizedName, string normalizedKnownKey)
    {
        if (string.Equals(normalizedName, normalizedKnownKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedKnownKey) || normalizedKnownKey.Length < 5)
        {
            return false;
        }

        return $" {normalizedName} ".Contains($" {normalizedKnownKey} ", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> IngredientKeysFor(CanonicalIngredient ingredient)
    {
        yield return KnowledgeNormalizer.NormalizeKey(ingredient.Name);
        yield return KnowledgeNormalizer.NormalizeKey(ingredient.CatalanName);
        yield return KnowledgeNormalizer.NormalizeKey(ingredient.SpanishName);

        foreach (var alias in ingredient.Aliases)
        {
            yield return KnowledgeNormalizer.NormalizeKey(alias);
        }
    }
}
