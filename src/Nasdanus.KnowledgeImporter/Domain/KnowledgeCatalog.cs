namespace Nasdanus.KnowledgeImporter.Domain;

public sealed class KnowledgeCatalog
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Generator { get; set; } = "Nasdanus.KnowledgeImporter";
    public List<CanonicalIngredient> Ingredients { get; set; } = [];
    public List<CanonicalProduct> Products { get; set; } = [];
    public List<FoodGroupDefinition> FoodGroups { get; set; } = [];
    public List<UnitDefinition> Units { get; set; } = [];
    public List<SeasonalityDefinition> Seasonality { get; set; } = [];
}

public sealed class FoodGroupDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShoppingCategory { get; set; } = string.Empty;
}

public sealed class UnitDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = UnitKind.Weight;
    public decimal? Grams { get; set; }
}

public sealed class SeasonalityDefinition
{
    public string IngredientId { get; set; } = string.Empty;
    public List<int> Months { get; set; } = [];
    public string Region { get; set; } = "ES";
}

public static class KnowledgeCategories
{
    public const string Vegetables = "vegetables";
    public const string Fruit = "fruit";
    public const string Meat = "meat";
    public const string Fish = "fish";
    public const string DairyEggs = "dairy-eggs";
    public const string Legumes = "legumes";
    public const string Grains = "grains";
    public const string Pantry = "pantry";
    public const string Spices = "spices";
    public const string Other = "other";

    public static readonly HashSet<string> All =
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

public static class KnowledgeUnits
{
    public const string Grams = "g";
    public const string Kilograms = "kg";
    public const string Millilitres = "ml";
    public const string Litres = "l";
    public const string Unit = "unit";
    public const string Tablespoon = "tbsp";
    public const string Teaspoon = "tsp";

    public static readonly HashSet<string> All =
    [
        Grams,
        Kilograms,
        Millilitres,
        Litres,
        Unit,
        Tablespoon,
        Teaspoon
    ];
}

public static class UnitKind
{
    public const string Weight = "weight";
    public const string Volume = "volume";
    public const string Count = "count";
}
