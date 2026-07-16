namespace Nasdanus.Domain;

public static class RecipeFoodClassifier
{
    public static RecipeFoodProfile Classify(Recipe recipe)
    {
        var text = NormalizedRecipeText(recipe);
        var ingredientNames = recipe.Ingredients
            .Select(ingredient => FoodText.Normalize(ingredient.DisplayName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var isFish = HasIngredientCategory(recipe, IngredientCategory.Fish)
            || ContainsAny(text, "peix", "salmo", "tonyina", "bacalla", "lluc", "gamba", "llagosti", "marisc", "fish", "salmon", "tuna", "cod", "hake", "shrimp");
        var isLegume = HasIngredientCategory(recipe, IngredientCategory.Legumes)
            || ContainsAny(text, "llent", "cigro", "cigrons", "mongeta", "fesol", "beans", "lentil", "chickpea");
        var isChicken = ingredientNames.Any(name => ContainsAny(name, "pollastre", "chicken", "gall dindi", "turkey"))
            || ContainsAny(text, "pollastre", "chicken", "gall dindi", "turkey");
        var isRedMeat = ingredientNames.Any(name => ContainsAny(name, "vedella", "porc", "xai", "beef", "pork", "lamb", "botifarra", "salsitxa", "bacon", "cansalada"))
            || ContainsAny(text, "vedella", "porc", "xai", "beef", "pork", "lamb", "botifarra", "salsitxa", "bacon", "cansalada");
        var hasEggs = ingredientNames.Any(name => ContainsAny(name, "ou", "ous", "egg", "eggs", "truita", "omelette"))
            || ContainsAny(text, "truita", "omelette");
        var isPasta = ingredientNames.Any(name => ContainsAny(name, "pasta", "macarro", "espagueti", "noodle", "tallarina", "ravioli"))
            || ContainsAny(text, "pasta", "macarro", "espagueti", "noodle", "tallarina", "ravioli");
        var isRice = ingredientNames.Any(name => ContainsAny(name, "arros", "rice", "risotto"))
            || ContainsAny(text, "arros", "rice", "risotto");
        var isFastFood = ContainsAny(text, "fast food", "pizza", "burger", "hamburguesa", "hot dog", "fregit");
        var isDessert = ContainsAny(text, "postres", "dessert", "pastis", "coca", "cookie", "galeta", "magdalena", "flam", "crema catalana", "brownie");
        var vegetableIngredientCount = recipe.Ingredients.Count(ingredient =>
            ingredient.Ingredient?.Category == IngredientCategory.Vegetables
            || ContainsAny(
                FoodText.Normalize(ingredient.DisplayName),
                "tomaquet",
                "ceba",
                "pastanaga",
                "carbasso",
                "pebrot",
                "espinac",
                "brocoli",
                "alberginia",
                "enciam",
                "cogombre",
                "verdura",
                "vegetable"));
        var isVegetableRich = vegetableIngredientCount >= 2
            || ContainsAny(text, "verdura", "verdures", "vegetable", "amanida", "salad", "escalivada");
        var isMeat = isChicken
            || isRedMeat
            || HasIngredientCategory(recipe, IngredientCategory.Meat)
            || ContainsAny(text, "carn", "meat");
        var isVegetarian = !isMeat && !isFish;

        return new RecipeFoodProfile(
            isFish,
            isLegume,
            isVegetableRich,
            isRedMeat,
            isChicken,
            isMeat,
            hasEggs,
            isPasta,
            isRice,
            isVegetarian,
            isFastFood,
            isDessert,
            vegetableIngredientCount,
            PrimaryGroup(isFish, isLegume, isChicken, isRedMeat, hasEggs, isPasta, isRice, isDessert, isFastFood, isVegetableRich, isMeat));
    }

    private static string PrimaryGroup(
        bool isFish,
        bool isLegume,
        bool isChicken,
        bool isRedMeat,
        bool hasEggs,
        bool isPasta,
        bool isRice,
        bool isDessert,
        bool isFastFood,
        bool isVegetableRich,
        bool isMeat)
    {
        if (isFish)
        {
            return FoodGroupKind.Fish;
        }

        if (isLegume)
        {
            return FoodGroupKind.Legumes;
        }

        if (isChicken)
        {
            return FoodGroupKind.Chicken;
        }

        if (isRedMeat)
        {
            return FoodGroupKind.RedMeat;
        }

        if (hasEggs)
        {
            return FoodGroupKind.Eggs;
        }

        if (isPasta)
        {
            return FoodGroupKind.Pasta;
        }

        if (isRice)
        {
            return FoodGroupKind.Rice;
        }

        if (isDessert)
        {
            return FoodGroupKind.Desserts;
        }

        if (isFastFood)
        {
            return FoodGroupKind.FastFood;
        }

        if (isVegetableRich)
        {
            return FoodGroupKind.VegetableRich;
        }

        return isMeat ? FoodGroupKind.Meat : FoodGroupKind.None;
    }

    private static bool HasIngredientCategory(Recipe recipe, string category) =>
        recipe.Ingredients.Any(ingredient => ingredient.Ingredient?.Category == category);

    private static string NormalizedRecipeText(Recipe recipe)
    {
        var tags = recipe.Tags.Select(tag => tag.Name);
        var planning = recipe.PlanningMetadata.Select(metadata => metadata.Value);
        var ingredients = recipe.Ingredients.Select(ingredient => ingredient.DisplayName);

        return FoodText.Normalize(string.Join(
            " ",
            new[] { recipe.Name, recipe.Category, recipe.Description }
                .Concat(tags)
                .Concat(planning)
                .Concat(ingredients)));
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
