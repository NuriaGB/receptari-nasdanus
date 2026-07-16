using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class PlanningSettingsService(BrowserAppStore store)
{
    public async Task<HouseholdPlanningSettings> GetSettingsAsync()
    {
        var state = await store.GetStateAsync();
        return Clone(state.PlanningSettings);
    }

    public async Task SaveSettingsAsync(HouseholdPlanningSettings settings)
    {
        var state = await store.GetStateAsync();
        state.PlanningSettings = Clone(settings);
        await store.SaveAsync();
    }

    private static HouseholdPlanningSettings Clone(HouseholdPlanningSettings? settings) => new()
    {
        NutritionGoals = new HouseholdNutritionGoals
        {
            TargetCaloriesPerPerson = settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000,
            MinimumProteinGramsPerPerson = settings?.NutritionGoals?.MinimumProteinGramsPerPerson ?? 85,
            TargetCarbohydrateGramsPerPerson = settings?.NutritionGoals?.TargetCarbohydrateGramsPerPerson ?? 240,
            TargetFatGramsPerPerson = settings?.NutritionGoals?.TargetFatGramsPerPerson ?? 70
        },
        WeeklyFoodRules = new WeeklyFoodRules
        {
            MinimumFishMeals = settings?.WeeklyFoodRules?.MinimumFishMeals ?? 2,
            MinimumLegumeMeals = settings?.WeeklyFoodRules?.MinimumLegumeMeals ?? 1,
            MaximumRedMeatMeals = settings?.WeeklyFoodRules?.MaximumRedMeatMeals ?? 1,
            MinimumVegetableRichMeals = settings?.WeeklyFoodRules?.MinimumVegetableRichMeals ?? 7,
            DayRules = settings?.WeeklyFoodRules?.DayRules
                ?.Select(rule => new DayFoodRule
                {
                    DayOfWeek = rule.DayOfWeek,
                    FoodGroup = rule.FoodGroup
                })
                .ToList() ?? []
        }
    };
}
