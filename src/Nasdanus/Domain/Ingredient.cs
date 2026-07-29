namespace Nasdanus.Domain;

public sealed class Ingredient
{
    public int Id { get; set; }
    public string KnowledgeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CatalanName { get; set; } = string.Empty;
    public string SpanishName { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Category { get; set; } = IngredientCategory.Other;
    public string Subcategory { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = "g";
    public string PantryCategory { get; set; } = ShoppingCategory.Other;
    public bool CanFreeze { get; set; }
    public string Seasonality { get; set; } = string.Empty;
    public IngredientNutrition? NutritionPer100Grams { get; set; }
    public string NutritionState { get; set; } = NutritionRecordState.Unspecified;
    public string NutritionSource { get; set; } = string.Empty;
    public string NutritionSourceId { get; set; } = string.Empty;
    public DateTimeOffset? NutritionLastUpdated { get; set; }
}

public sealed class IngredientNutrition
{
    public decimal? CaloriesKcal { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbohydrateGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FibreGrams { get; set; }
    public decimal? SugarGrams { get; set; }
    public decimal? SaltGrams { get; set; }

    public bool HasAnyValue =>
        CaloriesKcal is not null
        || ProteinGrams is not null
        || CarbohydrateGrams is not null
        || FatGrams is not null
        || FibreGrams is not null
        || SugarGrams is not null
        || SaltGrams is not null;
}

public sealed record IngredientNutritionManualEdit(
    IngredientNutrition Nutrition,
    string Source,
    string SourceId);

public sealed record IngredientKnowledgeManualEdit(
    string Name,
    string CatalanName,
    string SpanishName,
    IReadOnlyList<string> Aliases,
    string Category,
    string Subcategory,
    string DefaultUnit,
    string PantryCategory,
    bool CanFreeze,
    IngredientNutrition? Nutrition,
    string NutritionSource,
    string NutritionSourceId);

public sealed record IngredientKnowledgeManualSaveResult(
    Ingredient Ingredient,
    bool Created);

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public IngredientNutrition? NutritionPer100Grams { get; set; }
    public string NutritionSource { get; set; } = string.Empty;
}

public sealed class HouseholdIngredientPreference
{
    public string IngredientKnowledgeId { get; set; } = string.Empty;
    public bool IsFavourite { get; set; }
    public bool IsFrequentlyUsed { get; set; }
    public bool IsUsuallyAvailable { get; set; }
    public bool IsAlwaysInPantry { get; set; }
    public bool IsNormallyFrozen { get; set; }
    public string UseFrequency { get; set; } = IngredientUseFrequency.Occasional;
    public string PreferredAlias { get; set; } = string.Empty;
    public string HouseholdNotes { get; set; } = string.Empty;
}

public static class IngredientCategory
{
    public const string Vegetables = "Vegetables";
    public const string Fruit = "Fruit";
    public const string Meat = "Meat";
    public const string Fish = "Fish";
    public const string DairyEggs = "DairyEggs";
    public const string Legumes = "Legumes";
    public const string Grains = "Grains";
    public const string Pantry = "Pantry";
    public const string Spices = "Spices";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Vegetables,
        Fruit,
        Meat,
        Fish,
        DairyEggs,
        Legumes,
        Grains,
        Pantry,
        Spices,
        Other
    ];
}

public static class NutritionRecordState
{
    public const string Raw = "raw";
    public const string Cooked = "cooked";
    public const string Dry = "dry";
    public const string Canned = "canned";
    public const string Smoked = "smoked";
    public const string Unspecified = "unspecified";

    public static readonly string[] All =
    [
        Raw,
        Cooked,
        Dry,
        Canned,
        Smoked,
        Unspecified
    ];
}

public static class IngredientUseFrequency
{
    public const string Always = "Always";
    public const string Frequent = "Frequent";
    public const string Occasional = "Occasional";
    public const string Rare = "Rare";

    public static readonly string[] All =
    [
        Always,
        Frequent,
        Occasional,
        Rare
    ];

    public static string ToDisplayName(string frequency) => frequency switch
    {
        Always => "Sempre",
        Frequent => "Frequent",
        Occasional => "Ocasional",
        Rare => "Rar",
        _ => "Ocasional"
    };
}
