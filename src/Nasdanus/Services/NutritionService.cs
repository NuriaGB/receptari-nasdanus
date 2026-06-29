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

    public static string CompactNutritionText(NutritionTotals totals)
    {
        if (!totals.HasKnownNutrition)
        {
            return "Nutricio pendent";
        }

        return $"{Round(totals.CaloriesKcal)} kcal · P {Round(totals.ProteinGrams)} g · C {Round(totals.CarbohydrateGrams)} g · F {Round(totals.FatGrams)} g";
    }

    private static void AddIngredientNutrition(NutritionTotals totals, RecipeIngredient ingredient, decimal scale)
    {
        var nutrition = ingredient.Ingredient?.NutritionPer100Grams;
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

        return UnitToGrams(scaledQuantity, unit);
    }

    private static decimal? UnitToGrams(decimal quantity, string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "" => quantity,
            "g" => quantity,
            "gr" => quantity,
            "gram" => quantity,
            "grams" => quantity,
            "kg" => quantity * 1000m,
            "mg" => quantity / 1000m,
            _ => null
        };
    }

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
