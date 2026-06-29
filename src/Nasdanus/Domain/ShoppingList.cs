namespace Nasdanus.Domain;

public sealed class ShoppingList
{
    public int Id { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ShoppingListItem> Items { get; set; } = [];
}

public sealed class ShoppingListItem
{
    public int Id { get; set; }
    public int ShoppingListId { get; set; }
    public ShoppingList? ShoppingList { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ShoppingCategory.Other;
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public bool IsChecked { get; set; }
    public bool IsManual { get; set; }
    public bool IsHouseholdItem { get; set; }
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int Order { get; set; }
}

public sealed class ShoppingItemEditRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ShoppingCategory.Other;
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsHouseholdItem { get; set; }
    public int? RecipeId { get; set; }
}

public static class ShoppingCategory
{
    public const string Vegetables = "Vegetables";
    public const string Meat = "Meat";
    public const string Fish = "Fish";
    public const string DairyEggs = "DairyEggs";
    public const string Pantry = "Pantry";
    public const string Spices = "Spices";
    public const string Other = "Other";

    public static readonly string[] DisplayOrder =
    [
        Vegetables,
        Meat,
        Fish,
        DairyEggs,
        Pantry,
        Spices,
        Other
    ];

    public static string ToDisplayName(string category) => category switch
    {
        Vegetables => "🥬 Vegetables",
        Meat => "🥩 Meat",
        Fish => "🐟 Fish",
        DairyEggs => "🥚 Dairy & Eggs",
        Pantry => "🌾 Pantry",
        Spices => "🧂 Spices",
        _ => "🥫 Other"
    };
}
