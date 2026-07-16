using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class RecipeSuggestionService(BrowserAppStore store)
{
    public async Task<IReadOnlyList<RecipeSuggestion>> GetSuggestionsAsync(
        DateOnly date,
        MealKind mealKind,
        int limit = 10,
        int? replacingPlannedRecipeId = null)
    {
        var state = await store.GetStateAsync();
        var settings = state.PlanningSettings;
        var weekStart = PlannerService.WeekStart(date);
        var weekSlots = WeekSlots(state, weekStart).ToList();
        var plannedRecipeIds = weekSlots
            .SelectMany(slot => slot.PlannedRecipes)
            .Where(plannedRecipe => plannedRecipe.Id != replacingPlannedRecipeId)
            .Select(plannedRecipe => plannedRecipe.RecipeId)
            .ToHashSet();
        var status = BuildStatus(state, weekStart);
        var targetGroups = settings.WeeklyFoodRules.DayRules
            .FirstOrDefault(rule => rule.DayOfWeek == date.DayOfWeek)
            ?.FoodGroups ?? [];
        var recentGroups = RecentPrimaryGroups(state, date).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return state.Recipes
            .Where(recipe => !recipe.IsDraft)
            .Select(recipe => ScoreRecipe(
                store.CloneRecipe(recipe),
                date,
                mealKind,
                settings,
                status,
                targetGroups,
                plannedRecipeIds,
                recentGroups))
            .OrderByDescending(suggestion => suggestion.Score)
            .ThenBy(suggestion => suggestion.Recipe.Name)
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();
    }

    public async Task<WeeklyNutritionGoalStatus> GetWeekStatusAsync(DateOnly date)
    {
        var state = await store.GetStateAsync();
        var weekStart = PlannerService.WeekStart(date);
        return BuildStatus(state, weekStart);
    }

    private RecipeSuggestion ScoreRecipe(
        Recipe recipe,
        DateOnly date,
        MealKind mealKind,
        HouseholdPlanningSettings settings,
        WeeklyNutritionGoalStatus status,
        IReadOnlyList<string> targetGroups,
        HashSet<int> plannedRecipeIds,
        HashSet<string> recentGroups)
    {
        var profile = RecipeFoodClassifier.Classify(recipe);
        var recipeServings = recipe.Servings > 0 ? recipe.Servings : 1;
        var nutritionPerServing = NutritionService.PerServing(
            NutritionService.CalculateRecipe(recipe, recipeServings),
            recipeServings);
        var reasons = new List<string>();
        var score = 50m;
        var isHighProtein = nutritionPerServing.ProteinGrams >= 25m;
        var isQuickMeal = TotalMinutes(recipe) <= 30;
        var alreadyPlanned = plannedRecipeIds.Contains(recipe.Id);

        var matchedDayGroups = targetGroups
            .Where(group => profile.Matches(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matchedDayGroups.Count > 0)
        {
            score += 35;
            reasons.Add($"{string.Join(", ", matchedDayGroups.Select(FoodGroupKind.ToDisplayName))} for this day");
        }

        var proteinTarget = settings.NutritionGoals.MinimumProteinGramsPerPerson;
        var proteinDeficit = proteinTarget - status.AveragePerPersonPerDay.ProteinGrams;
        if (proteinDeficit > 0)
        {
            if (nutritionPerServing.ProteinGrams >= 30m)
            {
                score += 30;
                reasons.Add("Strong protein boost");
            }
            else if (nutritionPerServing.ProteinGrams >= 20m)
            {
                score += 18;
                reasons.Add("Helps protein");
            }
            else
            {
                score += Math.Min(10m, nutritionPerServing.ProteinGrams / 2m);
            }
        }

        AddFoodTargetSignals(profile, status, reasons, ref score);

        if (recipe.IsFavourite)
        {
            score += 8;
            reasons.Add("Favourite");
        }

        var cookedCount = recipe.CookingHistory.Count;
        if (cookedCount >= 2)
        {
            score += 6;
            reasons.Add("Often cooked");
        }

        var daysSinceCooked = DaysSinceCooked(recipe, date);
        if (daysSinceCooked is null)
        {
            score += 8;
            reasons.Add("Not cooked recently");
        }
        else if (daysSinceCooked <= 3)
        {
            score -= 35;
            reasons.Add("Cooked recently");
        }
        else if (daysSinceCooked >= 14)
        {
            score += 8;
            reasons.Add("Fresh rotation");
        }
        else if (daysSinceCooked >= 7)
        {
            score += 4;
        }

        if (alreadyPlanned)
        {
            score -= 30;
            reasons.Add("Already planned this week");
        }

        if (!string.IsNullOrWhiteSpace(profile.PrimaryGroup) && recentGroups.Contains(profile.PrimaryGroup))
        {
            score -= 12;
            reasons.Add("Similar to a recent meal");
        }

        if (MatchesMealKind(recipe, mealKind))
        {
            score += 8;
            reasons.Add($"Good for {mealKind.ToDisplayName().ToLowerInvariant()}");
        }
        else if (!string.IsNullOrWhiteSpace(recipe.Category))
        {
            score -= 5;
        }

        if (isQuickMeal)
        {
            score += 8;
            reasons.Add("Quick meal");
        }
        else if (TotalMinutes(recipe) <= 45)
        {
            score += 4;
        }
        else if (mealKind == MealKind.Dinner && TotalMinutes(recipe) > 60)
        {
            score -= 4;
        }

        if (!HelpsOpenFoodTargets(profile, status) && proteinDeficit > 0 && nutritionPerServing.ProteinGrams < 15m)
        {
            score -= 6;
            reasons.Add("Limited help for current goals");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Balanced option");
        }

        return new RecipeSuggestion(
            recipe,
            score,
            reasons.Take(3).ToList(),
            nutritionPerServing,
            profile,
            alreadyPlanned,
            isHighProtein,
            isQuickMeal);
    }

    private static void AddFoodTargetSignals(
        RecipeFoodProfile profile,
        WeeklyNutritionGoalStatus status,
        List<string> reasons,
        ref decimal score)
    {
        foreach (var target in status.FoodGroupTargets)
        {
            if (target.Target <= 0 || !profile.Matches(target.FoodGroup))
            {
                continue;
            }

            if (target.IsMaximum)
            {
                if (target.Current >= target.Target)
                {
                    score -= 20;
                    reasons.Add($"{FoodGroupKind.ToDisplayName(target.FoodGroup)} already high");
                }

                continue;
            }

            if (target.Current < target.Target)
            {
                score += target.FoodGroup switch
                {
                    FoodGroupKind.Fish => 25,
                    FoodGroupKind.Legumes => 24,
                    FoodGroupKind.VegetableRich => 18,
                    _ => 12
                };
                reasons.Add($"Helps {FoodGroupKind.ToDisplayName(target.FoodGroup).ToLowerInvariant()} target");
            }
        }
    }

    private WeeklyNutritionGoalStatus BuildStatus(LocalAppState state, DateOnly weekStart)
    {
        var days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var lunch = NutritionService.CalculateMeal(SlotOrEmpty(state, day, MealKind.Lunch));
                var dinner = NutritionService.CalculateMeal(SlotOrEmpty(state, day, MealKind.Dinner));
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

        var week = new WeekNutritionSummary(weekStart, days, weekTotals);
        var targets = state.PlanningSettings.WeeklyFoodRules.Targets
            .Where(target => !string.IsNullOrWhiteSpace(target.FoodGroup))
            .Select(target => new FoodGroupTargetStatus(
                target.FoodGroup,
                CountMealsForGroup(state, weekStart, target.FoodGroup),
                target.MealsPerWeek,
                target.RuleType == WeeklyFoodRuleType.Maximum))
            .ToList();

        if (targets.Count == 0)
        {
            targets =
            [
                new(FoodGroupKind.Fish, CountMealsForGroup(state, weekStart, FoodGroupKind.Fish), state.PlanningSettings.WeeklyFoodRules.MinimumFishMeals, false),
                new(FoodGroupKind.Legumes, CountMealsForGroup(state, weekStart, FoodGroupKind.Legumes), state.PlanningSettings.WeeklyFoodRules.MinimumLegumeMeals, false),
                new(FoodGroupKind.RedMeat, CountMealsForGroup(state, weekStart, FoodGroupKind.RedMeat), state.PlanningSettings.WeeklyFoodRules.MaximumRedMeatMeals, true),
                new(FoodGroupKind.VegetableRich, CountMealsForGroup(state, weekStart, FoodGroupKind.VegetableRich), state.PlanningSettings.WeeklyFoodRules.MinimumVegetableRichMeals, false)
            ];
        }

        return new WeeklyNutritionGoalStatus(
            week,
            NutritionService.AveragePerPersonPerDay(week),
            state.PlanningSettings,
            targets);
    }

    private static bool HelpsOpenFoodTargets(RecipeFoodProfile profile, WeeklyNutritionGoalStatus status) =>
        status.FoodGroupTargets.Any(target =>
            profile.Matches(target.FoodGroup)
            && !target.IsMaximum
            && target.Current < target.Target);

    private static int CountMealsForGroup(LocalAppState state, DateOnly weekStart, string foodGroup) =>
        WeekSlots(state, weekStart).Count(slot =>
            slot.PlannedRecipes
                .Select(plannedRecipe => plannedRecipe.Recipe ?? state.Recipes.FirstOrDefault(recipe => recipe.Id == plannedRecipe.RecipeId))
                .Where(recipe => recipe is not null)
                .Any(recipe => RecipeFoodClassifier.Classify(recipe!).Matches(foodGroup)));

    private static IEnumerable<string> RecentPrimaryGroups(LocalAppState state, DateOnly date)
    {
        var recentStart = date.AddDays(-3);
        var recentPlannedGroups = state.MealPlanSlots
            .Where(slot => slot.Date >= recentStart && slot.Date < date)
            .SelectMany(slot => slot.PlannedRecipes)
            .Select(plannedRecipe => plannedRecipe.Recipe ?? state.Recipes.FirstOrDefault(recipe => recipe.Id == plannedRecipe.RecipeId))
            .Where(recipe => recipe is not null)
            .Select(recipe => RecipeFoodClassifier.Classify(recipe!).PrimaryGroup);

        var recentCookedGroups = state.Recipes
            .Where(recipe => recipe.CookingHistory.Any(session =>
                DateOnly.FromDateTime(session.CookedAt.Date) >= recentStart
                && DateOnly.FromDateTime(session.CookedAt.Date) < date))
            .Select(recipe => RecipeFoodClassifier.Classify(recipe).PrimaryGroup);

        return recentPlannedGroups
            .Concat(recentCookedGroups)
            .Where(group => !string.IsNullOrWhiteSpace(group));
    }

    private static IEnumerable<MealPlanSlot> WeekSlots(LocalAppState state, DateOnly weekStart) =>
        state.MealPlanSlots.Where(slot => slot.Date >= weekStart && slot.Date <= weekStart.AddDays(6));

    private static MealPlanSlot SlotOrEmpty(LocalAppState state, DateOnly date, MealKind mealKind) =>
        state.MealPlanSlots.FirstOrDefault(slot => slot.Date == date && slot.MealKind == mealKind)
        ?? new MealPlanSlot { Date = date, MealKind = mealKind };

    private static bool MatchesMealKind(Recipe recipe, MealKind mealKind)
    {
        if (string.IsNullOrWhiteSpace(recipe.Category))
        {
            return false;
        }

        return string.Equals(recipe.Category, mealKind.ToDisplayName(), StringComparison.OrdinalIgnoreCase)
            || recipe.Category.Contains(mealKind.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int TotalMinutes(Recipe recipe) =>
        Math.Max(0, recipe.PreparationTimeMinutes) + Math.Max(0, recipe.CookingTimeMinutes);

    private static int? DaysSinceCooked(Recipe recipe, DateOnly date)
    {
        if (recipe.CookingHistory.Count == 0)
        {
            return null;
        }

        var lastCooked = recipe.CookingHistory.Max(session => DateOnly.FromDateTime(session.CookedAt.Date));
        return date.DayNumber - lastCooked.DayNumber;
    }
}
