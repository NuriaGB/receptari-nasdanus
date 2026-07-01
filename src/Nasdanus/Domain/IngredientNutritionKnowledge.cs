namespace Nasdanus.Domain;

public static class IngredientNutritionKnowledge
{
    private static readonly NutritionEntry[] Entries =
    [
        Entry(["oli", "olive oil", "aceite"], 884, 0, 0, 100),
        Entry(["mantega", "butter"], 717, 1, 0, 81),
        Entry(["sucre", "sugar"], 400, 0, 100, 0),
        Entry(["farina", "flour"], 364, 10, 76, 1),
        Entry(["arros", "rice"], 360, 7, 78, 1),
        Entry(["pasta", "macarrons", "espaguetis"], 371, 13, 75, 2),
        Entry(["pa ", "pa de", "bread"], 265, 9, 49, 3),
        Entry(["cigrons", "chickpeas"], 364, 19, 61, 6),
        Entry(["llenties", "lentils"], 352, 25, 60, 1),
        Entry(["mongetes", "beans"], 333, 21, 60, 1),
        Entry(["vedella", "beef"], 250, 26, 0, 15),
        Entry(["pollastre", "chicken"], 165, 31, 0, 4),
        Entry(["porc", "pork"], 242, 27, 0, 14),
        Entry(["salsitxa", "botifarra"], 300, 14, 2, 26),
        Entry(["salmo", "salmon"], 208, 20, 0, 13),
        Entry(["tonyina", "tuna"], 132, 28, 0, 1),
        Entry(["bacalla", "cod"], 82, 18, 0, 1),
        Entry(["lluc", "hake"], 86, 18, 0, 2),
        Entry(["gamba", "llagosti", "shrimp"], 99, 24, 0, 1),
        Entry(["ou", "ous", "egg"], 143, 13, 1, 10),
        Entry(["formatge", "cheese"], 400, 25, 1, 33),
        Entry(["mozzarella"], 280, 18, 3, 22),
        Entry(["llet", "milk"], 61, 3, 5, 3),
        Entry(["iogurt", "yogurt"], 60, 4, 5, 3),
        Entry(["nata", "cream"], 340, 2, 3, 36),
        Entry(["patata", "potato"], 77, 2, 17, 0),
        Entry(["tomaquet", "tomato"], 18, 1, 4, 0),
        Entry(["ceba", "onion"], 40, 1, 9, 0),
        Entry(["pastanaga", "carrot"], 41, 1, 10, 0),
        Entry(["carbasso", "zucchini"], 17, 1, 3, 0),
        Entry(["pebrot", "pepper"], 31, 1, 6, 0),
        Entry(["espinacs", "spinach"], 23, 3, 4, 0),
        Entry(["brocoli", "broccoli"], 34, 3, 7, 0),
        Entry(["alberginia", "eggplant"], 25, 1, 6, 0),
        Entry(["enciam", "lettuce"], 15, 1, 3, 0),
        Entry(["cogombre", "cucumber"], 15, 1, 4, 0),
        Entry(["all", "garlic"], 149, 6, 33, 1),
        Entry(["llimona", "lemon"], 29, 1, 9, 0),
        Entry(["poma", "apple"], 52, 0, 14, 0),
        Entry(["platan", "banana"], 89, 1, 23, 0),
        Entry(["xocolata", "chocolate"], 546, 5, 61, 31),
        Entry(["salsa de soja", "soy sauce"], 53, 8, 5, 1)
    ];

    public static IngredientNutrition? FindForName(string name)
    {
        var normalizedName = $" {FoodText.Normalize(name)} ";
        var entry = Entries.FirstOrDefault(entry => entry.Keywords.Any(keyword => MatchesKeyword(normalizedName, keyword)));
        return entry?.Nutrition is null
            ? null
            : new IngredientNutrition
            {
                CaloriesKcal = entry.Nutrition.CaloriesKcal,
                ProteinGrams = entry.Nutrition.ProteinGrams,
                CarbohydrateGrams = entry.Nutrition.CarbohydrateGrams,
                FatGrams = entry.Nutrition.FatGrams,
                FibreGrams = entry.Nutrition.FibreGrams,
                SugarGrams = entry.Nutrition.SugarGrams,
                SaltGrams = entry.Nutrition.SaltGrams
            };
    }

    private static bool MatchesKeyword(string normalizedName, string keyword) =>
        keyword.Length <= 3
            ? normalizedName.Contains($" {keyword} ", StringComparison.OrdinalIgnoreCase)
            : normalizedName.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static NutritionEntry Entry(string[] keywords, decimal calories, decimal protein, decimal carbs, decimal fat) =>
        new(
            keywords.Select(keyword => FoodText.Normalize(keyword)).ToArray(),
            new IngredientNutrition
            {
                CaloriesKcal = calories,
                ProteinGrams = protein,
                CarbohydrateGrams = carbs,
                FatGrams = fat
            });

    private sealed record NutritionEntry(string[] Keywords, IngredientNutrition Nutrition);
}
