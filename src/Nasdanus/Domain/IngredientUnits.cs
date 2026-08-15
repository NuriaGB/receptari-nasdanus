namespace Nasdanus.Domain;

public sealed record IngredientUnitOption(string Value, string Label);

public static class IngredientUnits
{
    public static readonly IReadOnlyList<IngredientUnitOption> All =
    [
        new(string.Empty, "Sense unitat"),
        new("g", "g"),
        new("kg", "kg"),
        new("mg", "mg"),
        new("ml", "ml"),
        new("cl", "cl"),
        new("l", "l"),
        new("culleradeta", "culleradeta"),
        new("cullerada", "cullerada"),
        new("got", "got"),
        new("polsim", "polsim"),
        new("pessic", "pessic"),
        new("grapat", "grapat"),
        new("unitat", "unitat"),
        new("dent", "dent"),
        new("fulla", "fulla"),
        new("tros", "tros"),
        new("trosset", "trosset"),
        new("rajolinet", "rajolinet"),
        new("paquet", "paquet"),
        new("pot", "pot"),
        new("llauna", "llauna"),
        new("sobre", "sobre"),
        new("porcio", "porcio"),
        new("barra", "barra"),
        new("branca", "branca")
    ];

    public static IReadOnlyList<IngredientUnitOption> OptionsIncluding(string? unit)
    {
        var normalized = Normalize(unit);
        if (string.IsNullOrWhiteSpace(normalized)
            || All.Any(option => string.Equals(option.Value, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return All;
        }

        return All
            .Concat([new IngredientUnitOption(normalized, $"{normalized} (existent)")])
            .ToList();
    }

    public static string Normalize(string? unit)
    {
        var normalized = FoodText.Normalize(unit ?? string.Empty);
        return normalized switch
        {
            "" => string.Empty,
            "gr" or "gram" or "grams" => "g",
            "kilogram" or "kilograms" or "quilo" or "quilos" => "kg",
            "milligram" or "milligrams" => "mg",
            "mililitre" or "mililitres" or "millilitre" or "millilitres" => "ml",
            "centilitre" or "centilitres" => "cl",
            "lt" or "litre" or "litres" => "l",
            "cullerades" or "tbsp" => "cullerada",
            "culleradetes" or "tsp" => "culleradeta",
            "vas" or "vasos" or "gots" => "got",
            "polsims" => "polsim",
            "pessics" => "pessic",
            "grapats" => "grapat",
            "unit" or "units" or "unitats" or "u" => "unitat",
            "dents" => "dent",
            "fulles" => "fulla",
            "trossos" => "tros",
            "trossets" => "trosset",
            "rajoli" or "rajolins" or "raig" or "rajos" => "rajolinet",
            "paquets" => "paquet",
            "pots" => "pot",
            "llaunes" => "llauna",
            "sobres" => "sobre",
            "porcions" => "porcio",
            "barres" => "barra",
            "branques" => "branca",
            _ => normalized
        };
    }

    public static string CanonicalizeForStorage(string? unit)
    {
        var value = unit?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = Normalize(value);
        return All.Any(option => string.Equals(option.Value, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : value;
    }

    public static string DisplayUnit(string unit, decimal? quantity)
    {
        var normalized = Normalize(unit);
        if (quantity is null || quantity.Value == 1)
        {
            return normalized;
        }

        return normalized switch
        {
            "unitat" => "unitats",
            "cullerada" => "cullerades",
            "culleradeta" => "culleradetes",
            "got" => "gots",
            "polsim" => "polsims",
            "pessic" => "pessics",
            "grapat" => "grapats",
            "dent" => "dents",
            "fulla" => "fulles",
            "tros" => "trossos",
            "trosset" => "trossets",
            "rajolinet" => "rajolins",
            "paquet" => "paquets",
            "pot" => "pots",
            "llauna" => "llaunes",
            "sobre" => "sobres",
            "porcio" => "porcions",
            "barra" => "barres",
            "branca" => "branques",
            _ => normalized
        };
    }
}
