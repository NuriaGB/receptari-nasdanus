using System.Text.Json.Serialization;

namespace Nasdanus.KnowledgeImporter.Domain;

public sealed class CanonicalIngredient
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CatalanName { get; set; } = string.Empty;
    public string SpanishName { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Category { get; set; } = KnowledgeCategories.Other;
    public string Subcategory { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = KnowledgeUnits.Grams;
    public bool CanFreeze { get; set; }
    public string PantryCategory { get; set; } = KnowledgeCategories.Other;
    public NutritionFacts? Nutrition { get; set; }
    public string NutritionState { get; set; } = NutritionRecordState.Unspecified;
    public string Source { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NutritionFacts
{
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbohydrates { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fibre { get; set; }
    public decimal? Sugar { get; set; }
    public decimal? Salt { get; set; }

    [JsonIgnore]
    public bool HasCoreMacros =>
        Calories is not null
        && Protein is not null
        && Carbohydrates is not null
        && Fat is not null;
}

public sealed class CanonicalProduct
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? IngredientId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public static class NutritionRecordState
{
    public const string Raw = "raw";
    public const string Cooked = "cooked";
    public const string Dry = "dry";
    public const string Canned = "canned";
    public const string Smoked = "smoked";
    public const string Unspecified = "unspecified";

    public static readonly HashSet<string> All =
    [
        Raw,
        Cooked,
        Dry,
        Canned,
        Smoked,
        Unspecified
    ];
}
