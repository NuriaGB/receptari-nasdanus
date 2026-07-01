using System.Globalization;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class NutritionService(BrowserAppStore store)
{
    public async Task<WeekNutritionSummary> CalculateWeekAsync(DateOnly date)
    {
        var state = await store.GetStateAsync();
        var weekStart = PlannerService.WeekStart(date);
        var days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var lunch = CalculateMeal(SlotFor(state, day, MealKind.Lunch));
                var dinner = CalculateMeal(SlotFor(state, day, MealKind.Dinner));
                var totals = new NutritionTotals();
                totals.Add(lunch.Totals);
                totals.Add(dinner.Totals);
                return new DayNutritionSummary(day, lunch, dinner, totals);
            })
            .ToList();

        var weekTotals = new NutritionTotals();
        foreach (var day in days)
        {
            weekTotals.Add(day.Totals);
        }

        return new WeekNutritionSummary(weekStart, days, weekTotals);
    }

    public static MealNutritionSummary CalculateMeal(MealPlanSlot? slot)
    {
        if (slot is null)
        {
            return new MealNutritionSummary(DateOnly.MinValue, MealKind.Lunch, [], new NutritionTotals());
        }

        var recipeSummaries = slot.PlannedRecipes
            .OrderBy(plannedRecipe => plannedRecipe.Order)
            .Where(plannedRecipe => plannedRecipe.Recipe is not null)
            .Select(plannedRecipe =>
            {
                var recipe = plannedRecipe.Recipe!;
                var servings = plannedRecipe.PlannedServings > 0
                    ? plannedRecipe.PlannedServings
                    : recipe.Servings;
                return new PlannedRecipeNutritionSummary(
                    plannedRecipe.Id,
                    recipe.Id,
                    recipe.Name,
                    servings,
                    CalculateRecipe(recipe, servings));
            })
            .ToList();

        var totals = new NutritionTotals();
        foreach (var recipe in recipeSummaries)
        {
            totals.Add(recipe.Totals);
        }

        return new MealNutritionSummary(slot.Date, slot.MealKind, recipeSummaries, totals);
    }

    public static NutritionTotals CalculateRecipe(Recipe recipe, int servings)
    {
        var scale = IngredientScaling.ScaleFactor(recipe.Servings, servings);
        var totals = new NutritionTotals();

        foreach (var ingredient in recipe.Ingredients.OrderBy(ingredient => ingredient.Order))
        {
            AddIngredientNutrition(totals, ingredient, scale);
        }

        return totals;
    }

    public static NutritionTotals PerServing(NutritionTotals totals, int servings) =>
        totals.DivideBy(servings > 0 ? servings : 1);

    public static NutritionTotals AveragePerDay(WeekNutritionSummary week) =>
        week.Totals.DivideBy(7);

    public static string CompactNutritionText(NutritionTotals totals)
    {
        if (!totals.HasKnownNutrition)
        {
            return "0 kcal · P 0 g · C 0 g · F 0 g · pendent";
        }

        return $"{Round(totals.CaloriesKcal)} kcal · P {Round(totals.ProteinGrams)} g · C {Round(totals.CarbohydrateGrams)} g · F {Round(totals.FatGrams)} g";
    }

    private static void AddIngredientNutrition(NutritionTotals totals, RecipeIngredient ingredient, decimal scale)
    {
        var linkedIngredient = ingredient.Ingredient;
        if (linkedIngredient is null || string.IsNullOrWhiteSpace(linkedIngredient.KnowledgeId))
        {
            totals.UnknownNutritionCount++;
            return;
        }

        var nutrition = linkedIngredient.NutritionPer100Grams;
        if (nutrition is null || !nutrition.HasAnyValue)
        {
            totals.UnknownNutritionCount++;
            return;
        }

        var grams = QuantityInGrams(ingredient, scale);
        if (grams is null)
        {
            totals.UnknownQuantityCount++;
            return;
        }

        var factor = grams.Value / 100m;
        totals.KnownIngredientCount++;
        totals.CaloriesKcal += (nutrition.CaloriesKcal ?? 0) * factor;
        totals.ProteinGrams += (nutrition.ProteinGrams ?? 0) * factor;
        totals.CarbohydrateGrams += (nutrition.CarbohydrateGrams ?? 0) * factor;
        totals.FatGrams += (nutrition.FatGrams ?? 0) * factor;
        totals.FibreGrams += (nutrition.FibreGrams ?? 0) * factor;
        totals.SugarGrams += (nutrition.SugarGrams ?? 0) * factor;
        totals.SaltGrams += (nutrition.SaltGrams ?? 0) * factor;
    }

    private static decimal? QuantityInGrams(RecipeIngredient ingredient, decimal scale)
    {
        var quantity = IngredientScaling.ParseQuantity(ingredient.Quantity);
        if (quantity is null)
        {
            return null;
        }

        var scaledQuantity = IngredientScaling.ScaleQuantity(quantity.Value, ingredient.ScalingMode, scale);
        var unit = string.IsNullOrWhiteSpace(ingredient.Unit)
            ? ingredient.Ingredient?.DefaultUnit ?? string.Empty
            : ingredient.Unit;

        return UnitToGrams(scaledQuantity, unit, ingredient.DisplayName);
    }

    private static decimal? UnitToGrams(decimal quantity, string unit, string ingredientName)
    {
        var normalized = FoodText.Normalize(unit);
        return normalized switch
        {
            "" => quantity,
            "g" => quantity,
            "gr" => quantity,
            "gram" => quantity,
            "grams" => quantity,
            "kg" => quantity * 1000m,
            "mg" => quantity / 1000m,
            "ml" => quantity,
            "cl" => quantity * 10m,
            "l" => quantity * 1000m,
            "cullerada" => quantity * 15m,
            "cullerades" => quantity * 15m,
            "tbsp" => quantity * 15m,
            "culleradeta" => quantity * 5m,
            "culleradetes" => quantity * 5m,
            "tsp" => quantity * 5m,
            "dent" => quantity * 5m,
            "dents" => quantity * 5m,
            "grapat" => quantity * 30m,
            "grapats" => quantity * 30m,
            "trosset" => quantity * 5m,
            "trossets" => quantity * 5m,
            "fulla" => quantity * 2m,
            "fulles" => quantity * 2m,
            "unit" => UnitWeightInGrams(ingredientName) is decimal canonicalUnitWeight ? quantity * canonicalUnitWeight : null,
            "units" => UnitWeightInGrams(ingredientName) is decimal canonicalUnitsWeight ? quantity * canonicalUnitsWeight : null,
            "unitat" => UnitWeightInGrams(ingredientName) is decimal unitWeight ? quantity * unitWeight : null,
            "unitats" => UnitWeightInGrams(ingredientName) is decimal unitsWeight ? quantity * unitsWeight : null,
            "u" => UnitWeightInGrams(ingredientName) is decimal shortUnitWeight ? quantity * shortUnitWeight : null,
            _ => null
        };
    }

    private static decimal? UnitWeightInGrams(string ingredientName)
    {
        var name = FoodText.Normalize(ingredientName);
        if (ContainsAny(name, "ou", "egg"))
        {
            return 50m;
        }

        if (ContainsAny(name, "ceba", "onion"))
        {
            return 150m;
        }

        if (ContainsAny(name, "pastanaga", "carrot"))
        {
            return 60m;
        }

        if (ContainsAny(name, "carbasso", "zucchini"))
        {
            return 200m;
        }

        if (ContainsAny(name, "tomaquet", "tomato"))
        {
            return 120m;
        }

        if (ContainsAny(name, "patata", "potato"))
        {
            return 150m;
        }

        if (ContainsAny(name, "pebrot", "pepper"))
        {
            return 120m;
        }

        if (ContainsAny(name, "llimona", "lemon"))
        {
            return 60m;
        }

        if (ContainsAny(name, "poma", "apple"))
        {
            return 150m;
        }

        if (ContainsAny(name, "platan", "banana"))
        {
            return 120m;
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static MealPlanSlot? SlotFor(LocalAppState state, DateOnly date, MealKind mealKind) =>
        state.MealPlanSlots.FirstOrDefault(slot => slot.Date == date && slot.MealKind == mealKind);

    private static string Round(decimal value) =>
        value.ToString("0", CultureInfo.InvariantCulture);
}

public sealed class IngredientNutritionImportService(IEnumerable<IIngredientNutritionProvider> providers)
    : IIngredientNutritionImportService
{
    public async Task<IReadOnlyList<IngredientNutritionCandidate>> SearchProvidersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IngredientNutritionCandidate>();
        foreach (var provider in providers)
        {
            var providerResults = await provider.SearchAsync(query, cancellationToken);
            results.AddRange(providerResults);
        }

        return results;
    }
}
