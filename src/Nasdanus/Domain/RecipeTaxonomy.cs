namespace Nasdanus.Domain;

public static class RecipeCategory
{
    public const string Breakfast = "Esmorzar";
    public const string Lunch = "Dinar";
    public const string Dinner = "Sopar";
    public const string Salad = "Amanida";
    public const string Soup = "Sopa / crema";
    public const string Vegetables = "Verdura";
    public const string Legumes = "Llegums";
    public const string Rice = "Arros";
    public const string Pasta = "Pasta";
    public const string Fish = "Peix";
    public const string Seafood = "Marisc";
    public const string Meat = "Carn";
    public const string Poultry = "Pollastre / au";
    public const string Eggs = "Ous";
    public const string Sandwich = "Entrepa / wrap";
    public const string HomemadeFastFood = "Fast food casola";
    public const string Side = "Guarnicio";
    public const string Sauce = "Salsa";
    public const string Dough = "Pa / masses";
    public const string Dessert = "Postres";
    public const string BreakfastSnack = "Esmorzar / berenar";

    public static readonly string[] All =
    [
        Breakfast,
        Lunch,
        Dinner,
        Salad,
        Soup,
        Vegetables,
        Legumes,
        Rice,
        Pasta,
        Fish,
        Seafood,
        Meat,
        Poultry,
        Eggs,
        Sandwich,
        HomemadeFastFood,
        Side,
        Sauce,
        Dough,
        Dessert,
        BreakfastSnack
    ];

    public static IReadOnlyList<string> Parse(string categoryText) =>
        Split(categoryText)
            .Select(Normalize)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string Format(IEnumerable<string> categories) =>
        string.Join(", ", categories
            .Select(Normalize)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(CategorySortOrder)
            .ThenBy(category => category, StringComparer.OrdinalIgnoreCase));

    public static bool Contains(string categoryText, string category) =>
        !string.IsNullOrWhiteSpace(category)
        && Parse(categoryText).Any(existing => Same(existing, category));

    public static string Normalize(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        var trimmed = category.Trim();
        return All.FirstOrDefault(existing => Same(existing, trimmed)) ?? trimmed;
    }

    public static IEnumerable<string> Split(string categoryText) =>
        categoryText.Split([',', ';', '|', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Same(string left, string right) =>
        string.Equals(FoodText.Normalize(left), FoodText.Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static int CategorySortOrder(string category)
    {
        var normalized = FoodText.Normalize(category);
        var index = Array.FindIndex(All, existing => FoodText.Normalize(existing) == normalized);
        return index < 0 ? All.Length : index;
    }
}

public static class RecipeTagConventions
{
    public const string EquipmentPrefix = "equip:";

    private static readonly string[] EquipmentSignals =
    [
        "air fryer",
        "barbacoa",
        "batedora",
        "batedor",
        "bbq",
        "cassola",
        "colador",
        "forn",
        "fregidora",
        "grill",
        "microones",
        "motlle",
        "olla",
        "paella",
        "picadora",
        "planxa",
        "robot",
        "safata",
        "thermomix",
        "turmix",
        "vapor",
        "vaporera",
        "varetes",
        "wok"
    ];

    public static bool IsEquipmentTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var trimmed = tag.Trim();
        if (trimmed.StartsWith(EquipmentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(RemoveEquipmentPrefix(trimmed));
        }

        var normalized = FoodText.Normalize(trimmed);
        return EquipmentSignals.Any(signal => normalized.Contains(FoodText.Normalize(signal), StringComparison.OrdinalIgnoreCase));
    }

    public static string EquipmentTag(string equipment)
    {
        var cleaned = RemoveEquipmentPrefix(equipment);
        return string.IsNullOrWhiteSpace(cleaned)
            ? string.Empty
            : $"{EquipmentPrefix}{cleaned.Trim()}";
    }

    public static string RemoveEquipmentPrefix(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        var trimmed = tag.Trim();
        return trimmed.StartsWith(EquipmentPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[EquipmentPrefix.Length..].Trim()
            : trimmed;
    }
}
