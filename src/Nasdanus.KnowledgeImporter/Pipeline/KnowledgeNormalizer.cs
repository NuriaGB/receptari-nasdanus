using System.Globalization;
using System.Text;
using Nasdanus.KnowledgeImporter.Domain;

namespace Nasdanus.KnowledgeImporter.Pipeline;

public sealed class KnowledgeNormalizer
{
    public KnowledgeCatalog Normalize(IEnumerable<ProviderExportResult> providerExports)
    {
        var catalog = CreateBaseCatalog();
        var ingredientsByKey = new Dictionary<string, CanonicalIngredient>(StringComparer.OrdinalIgnoreCase);

        foreach (var providerExport in providerExports)
        {
            foreach (var providerIngredient in providerExport.Ingredients)
            {
                var ingredient = NormalizeIngredient(providerIngredient);
                var key = NormalizeKey(ingredient.Name);
                if (ingredientsByKey.TryGetValue(key, out var existing))
                {
                    Merge(existing, ingredient);
                    continue;
                }

                ingredientsByKey[key] = ingredient;
                catalog.Ingredients.Add(ingredient);
            }

            catalog.Products.AddRange(providerExport.Products);
        }

        catalog.Ingredients = catalog.Ingredients
            .OrderBy(ingredient => ingredient.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        catalog.Products = catalog.Products
            .OrderBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return catalog;
    }

    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static CanonicalIngredient NormalizeIngredient(ProviderIngredient source)
    {
        var name = source.Name.Trim();
        var aliases = source.Aliases
            .Append(name)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(alias => alias, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new CanonicalIngredient
        {
            Id = StableIngredientId(name),
            Name = name,
            Aliases = aliases,
            Category = NormalizeCategory(source.Category),
            DefaultUnit = NormalizeUnit(source.DefaultUnit),
            CanFreeze = source.CanFreeze ?? false,
            PantryCategory = NormalizeCategory(source.PantryCategory),
            Nutrition = source.Nutrition,
            Source = source.Provider,
            SourceId = source.ProviderId,
            LastUpdated = source.LastUpdated
        };
    }

    private static void Merge(CanonicalIngredient target, CanonicalIngredient source)
    {
        target.Aliases = target.Aliases
            .Concat(source.Aliases)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(alias => alias, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        target.Nutrition ??= source.Nutrition;
        target.CanFreeze = target.CanFreeze || source.CanFreeze;
        target.LastUpdated = target.LastUpdated > source.LastUpdated ? target.LastUpdated : source.LastUpdated;
    }

    private static string StableIngredientId(string name)
    {
        var key = NormalizeKey(name)
            .Replace(" ", "-", StringComparison.OrdinalIgnoreCase)
            .Replace("/", "-", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(key) ? "ingredient-unknown" : key;
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = NormalizeKey(category).Replace(" ", "-", StringComparison.OrdinalIgnoreCase);
        return KnowledgeCategories.All.Contains(normalized) ? normalized : KnowledgeCategories.Other;
    }

    private static string NormalizeUnit(string unit)
    {
        var normalized = NormalizeKey(unit);
        return KnowledgeUnits.All.Contains(normalized) ? normalized : KnowledgeUnits.Grams;
    }

    private static KnowledgeCatalog CreateBaseCatalog() => new()
    {
        FoodGroups =
        [
            Group(KnowledgeCategories.Vegetables, "Vegetables", "Vegetables"),
            Group(KnowledgeCategories.Fruit, "Fruit", "Vegetables"),
            Group(KnowledgeCategories.Meat, "Meat", "Meat"),
            Group(KnowledgeCategories.Fish, "Fish", "Fish"),
            Group(KnowledgeCategories.DairyEggs, "Dairy & Eggs", "Dairy & Eggs"),
            Group(KnowledgeCategories.Legumes, "Legumes", "Pantry"),
            Group(KnowledgeCategories.Grains, "Grains", "Pantry"),
            Group(KnowledgeCategories.Pantry, "Pantry", "Pantry"),
            Group(KnowledgeCategories.Spices, "Spices", "Spices"),
            Group(KnowledgeCategories.Other, "Other", "Other")
        ],
        Units =
        [
            Unit(KnowledgeUnits.Grams, "grams", UnitKind.Weight, 1),
            Unit(KnowledgeUnits.Kilograms, "kilograms", UnitKind.Weight, 1000),
            Unit(KnowledgeUnits.Millilitres, "millilitres", UnitKind.Volume, 1),
            Unit(KnowledgeUnits.Litres, "litres", UnitKind.Volume, 1000),
            Unit(KnowledgeUnits.Unit, "unit", UnitKind.Count, null),
            Unit(KnowledgeUnits.Tablespoon, "tablespoon", UnitKind.Volume, 15),
            Unit(KnowledgeUnits.Teaspoon, "teaspoon", UnitKind.Volume, 5)
        ]
    };

    private static FoodGroupDefinition Group(string id, string name, string shoppingCategory) => new()
    {
        Id = id,
        Name = name,
        ShoppingCategory = shoppingCategory
    };

    private static UnitDefinition Unit(string id, string name, string kind, decimal? grams) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
        Grams = grams
    };
}
