using Nasdanus.KnowledgeImporter.Domain;

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
}
