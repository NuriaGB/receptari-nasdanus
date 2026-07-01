using Nasdanus.KnowledgeImporter.Domain;
using Nasdanus.KnowledgeImporter.Pipeline;

namespace Nasdanus.KnowledgeImporter.Providers;

public sealed class LocalSeedKnowledgeProvider : IKnowledgeProvider
{
    private static readonly ProviderIngredient[] Ingredients =
    [
        Ingredient("oli-oliva", "Oli d'oliva", ["Olive oil", "Aceite de oliva", "Oli"], KnowledgeCategories.Pantry, KnowledgeUnits.Millilitres, false, KnowledgeCategories.Pantry, 884, 0, 0, 100),
        Ingredient("mantega", "Mantega", ["Butter", "Mantequilla"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Grams, true, KnowledgeCategories.DairyEggs, 717, 1, 0, 81),
        Ingredient("sucre", "Sucre", ["Sugar", "Azucar"], KnowledgeCategories.Pantry, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 400, 0, 100, 0),
        Ingredient("farina", "Farina", ["Flour", "Harina", "Farina de blat"], KnowledgeCategories.Grains, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 364, 10, 76, 1),
        Ingredient("arros", "Arros", ["Rice", "Arroz"], KnowledgeCategories.Grains, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 360, 7, 78, 1),
        Ingredient("pasta", "Pasta", ["Macarrons", "Espaguetis", "Macaroni", "Spaghetti"], KnowledgeCategories.Grains, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 371, 13, 75, 2),
        Ingredient("pa", "Pa", ["Bread", "Pan"], KnowledgeCategories.Grains, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 265, 9, 49, 3),
        Ingredient("cigrons", "Cigrons", ["Chickpeas", "Garbanzos"], KnowledgeCategories.Legumes, KnowledgeUnits.Grams, true, KnowledgeCategories.Pantry, 364, 19, 61, 6),
        Ingredient("llenties", "Llenties", ["Lentils", "Lentejas"], KnowledgeCategories.Legumes, KnowledgeUnits.Grams, true, KnowledgeCategories.Pantry, 352, 25, 60, 1),
        Ingredient("mongetes", "Mongetes", ["Beans", "Judias", "Alubias"], KnowledgeCategories.Legumes, KnowledgeUnits.Grams, true, KnowledgeCategories.Pantry, 333, 21, 60, 1),
        Ingredient("vedella", "Vedella", ["Beef", "Ternera"], KnowledgeCategories.Meat, KnowledgeUnits.Grams, true, KnowledgeCategories.Meat, 250, 26, 0, 15),
        Ingredient("pit-pollastre", "Pit de pollastre", ["Pits de pollastre", "Pollastre", "Chicken breast", "Pechuga de pollo", "Pechugas de pollo", "Filet de pollastre"], KnowledgeCategories.Meat, KnowledgeUnits.Grams, true, KnowledgeCategories.Meat, 165, 31, 0, 4),
        Ingredient("porc", "Porc", ["Pork", "Cerdo"], KnowledgeCategories.Meat, KnowledgeUnits.Grams, true, KnowledgeCategories.Meat, 242, 27, 0, 14),
        Ingredient("botifarra", "Botifarra", ["Salsitxa", "Sausage", "Butifarra"], KnowledgeCategories.Meat, KnowledgeUnits.Grams, true, KnowledgeCategories.Meat, 300, 14, 2, 26),
        Ingredient("salmo", "Salmo", ["Salmon", "Salmon", "Salmó"], KnowledgeCategories.Fish, KnowledgeUnits.Grams, true, KnowledgeCategories.Fish, 208, 20, 0, 13),
        Ingredient("tonyina", "Tonyina", ["Tuna", "Atun"], KnowledgeCategories.Fish, KnowledgeUnits.Grams, true, KnowledgeCategories.Fish, 132, 28, 0, 1),
        Ingredient("bacalla", "Bacalla", ["Cod", "Bacalao", "Bacallà"], KnowledgeCategories.Fish, KnowledgeUnits.Grams, true, KnowledgeCategories.Fish, 82, 18, 0, 1),
        Ingredient("lluc", "Lluca", ["Hake", "Merluza", "Lluc"], KnowledgeCategories.Fish, KnowledgeUnits.Grams, true, KnowledgeCategories.Fish, 86, 18, 0, 2),
        Ingredient("gambes", "Gambes", ["Gamba", "Llagosti", "Shrimp", "Langostino"], KnowledgeCategories.Fish, KnowledgeUnits.Grams, true, KnowledgeCategories.Fish, 99, 24, 0, 1),
        Ingredient("ou", "Ou", ["Ous", "Egg", "Eggs", "Huevo", "Huevos"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Unit, false, KnowledgeCategories.DairyEggs, 143, 13, 1, 10),
        Ingredient("formatge", "Formatge", ["Cheese", "Queso"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Grams, true, KnowledgeCategories.DairyEggs, 400, 25, 1, 33),
        Ingredient("mozzarella", "Mozzarella", ["Mozzarella cheese"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Grams, true, KnowledgeCategories.DairyEggs, 280, 18, 3, 22),
        Ingredient("llet", "Llet", ["Milk", "Leche"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Millilitres, false, KnowledgeCategories.DairyEggs, 61, 3, 5, 3),
        Ingredient("iogurt", "Iogurt", ["Yogurt", "Yoghurt", "Yogur"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Grams, false, KnowledgeCategories.DairyEggs, 60, 4, 5, 3),
        Ingredient("nata", "Nata", ["Cream", "Crema de leche"], KnowledgeCategories.DairyEggs, KnowledgeUnits.Millilitres, false, KnowledgeCategories.DairyEggs, 340, 2, 3, 36),
        Ingredient("patata", "Patata", ["Patates", "Potato", "Potatoes"], KnowledgeCategories.Vegetables, KnowledgeUnits.Grams, false, KnowledgeCategories.Vegetables, 77, 2, 17, 0),
        Ingredient("tomaquet", "Tomaquet", ["Tomato", "Tomate", "Tomaquets", "Tomates", "Tomàquet"], KnowledgeCategories.Vegetables, KnowledgeUnits.Grams, false, KnowledgeCategories.Vegetables, 18, 1, 4, 0),
        Ingredient("ceba", "Ceba", ["Onion", "Cebolla", "Cebes"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 40, 1, 9, 0),
        Ingredient("pastanaga", "Pastanaga", ["Carrot", "Zanahoria", "Pastanagues"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 41, 1, 10, 0),
        Ingredient("carbasso", "Carbasso", ["Zucchini", "Courgette", "Calabacin", "Carbassó"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 17, 1, 3, 0),
        Ingredient("pebrot", "Pebrot", ["Pepper", "Pimiento", "Pebrots"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 31, 1, 6, 0),
        Ingredient("espinacs", "Espinacs", ["Spinach", "Espinacas"], KnowledgeCategories.Vegetables, KnowledgeUnits.Grams, false, KnowledgeCategories.Vegetables, 23, 3, 4, 0),
        Ingredient("brocoli", "Brocoli", ["Broccoli", "Brocoli", "Bròcoli"], KnowledgeCategories.Vegetables, KnowledgeUnits.Grams, false, KnowledgeCategories.Vegetables, 34, 3, 7, 0),
        Ingredient("alberginia", "Alberginia", ["Eggplant", "Aubergine", "Berenjena"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 25, 1, 6, 0),
        Ingredient("enciam", "Enciam", ["Lettuce", "Lechuga"], KnowledgeCategories.Vegetables, KnowledgeUnits.Grams, false, KnowledgeCategories.Vegetables, 15, 1, 3, 0),
        Ingredient("cogombre", "Cogombre", ["Cucumber", "Pepino"], KnowledgeCategories.Vegetables, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 15, 1, 4, 0),
        Ingredient("all", "All", ["Garlic", "Ajo", "Dents d'all", "Dent d'all"], KnowledgeCategories.Spices, KnowledgeUnits.Unit, false, KnowledgeCategories.Spices, 149, 6, 33, 1),
        Ingredient("llimona", "Llimona", ["Lemon", "Limon"], KnowledgeCategories.Fruit, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 29, 1, 9, 0),
        Ingredient("poma", "Poma", ["Apple", "Manzana"], KnowledgeCategories.Fruit, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 52, 0, 14, 0),
        Ingredient("platan", "Platan", ["Banana", "Platano", "Plàtan"], KnowledgeCategories.Fruit, KnowledgeUnits.Unit, false, KnowledgeCategories.Vegetables, 89, 1, 23, 0),
        Ingredient("xocolata", "Xocolata", ["Chocolate"], KnowledgeCategories.Pantry, KnowledgeUnits.Grams, false, KnowledgeCategories.Pantry, 546, 5, 61, 31),
        Ingredient("salsa-soja", "Salsa de soja", ["Soy sauce", "Salsa soja"], KnowledgeCategories.Pantry, KnowledgeUnits.Millilitres, false, KnowledgeCategories.Pantry, 53, 8, 5, 1)
    ];

    public string ProviderId => "nasdanus-local-seed";
    public string ProviderName => "Nasdanus local seed";

    public Task<IReadOnlyList<ProviderIngredientSearchResult>> SearchIngredientsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = KnowledgeNormalizer.NormalizeKey(query);
        var results = Ingredients
            .Where(ingredient => string.IsNullOrWhiteSpace(normalizedQuery)
                || KnowledgeNormalizer.NormalizeKey(ingredient.Name).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || ingredient.Aliases.Any(alias => KnowledgeNormalizer.NormalizeKey(alias).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(ingredient => new ProviderIngredientSearchResult
            {
                Provider = ProviderId,
                ProviderId = ingredient.ProviderId,
                Name = ingredient.Name
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ProviderIngredientSearchResult>>(results);
    }

    public Task<ProviderIngredient?> DownloadIngredientAsync(
        string providerIngredientId,
        CancellationToken cancellationToken = default)
    {
        var ingredient = Ingredients.FirstOrDefault(ingredient =>
            string.Equals(ingredient.ProviderId, providerIngredientId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<ProviderIngredient?>(ingredient);
    }

    public Task<ProviderExportResult> ExportAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProviderExportResult
        {
            Provider = ProviderId,
            Ingredients = Ingredients
        });

    private static ProviderIngredient Ingredient(
        string providerId,
        string name,
        string[] aliases,
        string category,
        string defaultUnit,
        bool canFreeze,
        string pantryCategory,
        decimal calories,
        decimal protein,
        decimal carbohydrates,
        decimal fat) => new()
    {
        Provider = "nasdanus-local-seed",
        ProviderId = providerId,
        Name = name,
        Aliases = aliases.ToList(),
        Category = category,
        DefaultUnit = defaultUnit,
        CanFreeze = canFreeze,
        PantryCategory = pantryCategory,
        Nutrition = new NutritionFacts
        {
            Calories = calories,
            Protein = protein,
            Carbohydrates = carbohydrates,
            Fat = fat
        }
    };
}
