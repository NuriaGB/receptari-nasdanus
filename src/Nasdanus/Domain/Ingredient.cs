namespace Nasdanus.Domain;

public sealed class Ingredient
{
    public int Id { get; set; }
    public string KnowledgeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Category { get; set; } = IngredientCategory.Other;
    public string DefaultUnit { get; set; } = "g";
    public string PantryCategory { get; set; } = ShoppingCategory.Other;
    public bool CanFreeze { get; set; }
    public string Seasonality { get; set; } = string.Empty;
    public IngredientNutrition? NutritionPer100Grams { get; set; }
    public string NutritionSource { get; set; } = string.Empty;
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
