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
        General = new HouseholdGeneralSettings
        {
            HouseholdName = settings?.General?.HouseholdName ?? "Nasdanus",
            DefaultLanguage = settings?.General?.DefaultLanguage ?? HouseholdLanguage.Catalan,
            MeasurementSystem = settings?.General?.MeasurementSystem ?? MeasurementSystemKind.Metric,
            DefaultServings = settings?.General?.DefaultServings ?? 4,
            WeekStartsOn = settings?.General?.WeekStartsOn ?? DayOfWeek.Monday
        },
        Members = settings?.Members?
            .Select(member => new HouseholdMemberProfile
            {
                Id = member.Id,
                Name = member.Name,
                DateOfBirth = member.DateOfBirth,
                MeasurementDate = member.MeasurementDate,
                HeightCentimeters = member.HeightCentimeters,
                WeightKilograms = member.WeightKilograms,
                BodyFatPercentage = member.BodyFatPercentage,
                Sex = member.Sex,
                CurrentLifeStage = member.CurrentLifeStage,
                ActivityLevel = member.ActivityLevel,
                WeeklyExercise = member.WeeklyExercise,
                Occupation = member.Occupation,
                HealthNotes = member.HealthNotes,
                FavouriteFoods = member.FavouriteFoods,
                FoodsToAvoid = member.FoodsToAvoid,
                FoodsToEncourage = member.FoodsToEncourage,
                SpiceTolerance = member.SpiceTolerance,
                CookingPreferences = member.CookingPreferences,
                NutritionGoals = member.NutritionGoals.ToList(),
                CustomNutritionGoals = member.CustomNutritionGoals,
                MeasurementHistory = member.MeasurementHistory?
                    .Select(measurement => new BodyMeasurement
                    {
                        Id = measurement.Id,
                        Date = measurement.Date,
                        WeightKilograms = measurement.WeightKilograms,
                        HeightCentimeters = measurement.HeightCentimeters,
                        BodyFatPercentage = measurement.BodyFatPercentage,
                        Notes = measurement.Notes
                    })
                    .ToList() ?? []
            })
            .ToList() ?? HouseholdMemberDefaults.Create(),
        NutritionGoals = new HouseholdNutritionGoals
        {
            GoalScope = settings?.NutritionGoals?.GoalScope ?? NutritionGoalScope.WeeklyAverage,
            MacroMode = NutritionMacroMode.PercentageDistribution,
            TargetCaloriesPerPerson = settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000,
            MinimumProteinGramsPerPerson = CalculateMacroGrams(settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000, settings?.NutritionGoals?.ProteinPercent ?? 30, 4),
            TargetCarbohydrateGramsPerPerson = CalculateMacroGrams(settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000, settings?.NutritionGoals?.CarbohydratePercent ?? 35, 4),
            TargetFatGramsPerPerson = CalculateMacroGrams(settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000, settings?.NutritionGoals?.FatPercent ?? 35, 9),
            TargetFibreGramsPerPerson = settings?.NutritionGoals?.TargetFibreGramsPerPerson ?? 30,
            ProteinPercent = settings?.NutritionGoals?.ProteinPercent ?? 30,
            CarbohydratePercent = settings?.NutritionGoals?.CarbohydratePercent ?? 35,
            FatPercent = settings?.NutritionGoals?.FatPercent ?? 35
        },
        WeeklyFoodRules = new WeeklyFoodRules
        {
            MinimumFishMeals = settings?.WeeklyFoodRules?.MinimumFishMeals ?? 2,
            MinimumLegumeMeals = settings?.WeeklyFoodRules?.MinimumLegumeMeals ?? 1,
            MaximumRedMeatMeals = settings?.WeeklyFoodRules?.MaximumRedMeatMeals ?? 1,
            MinimumVegetableRichMeals = settings?.WeeklyFoodRules?.MinimumVegetableRichMeals ?? 7,
            Targets = settings?.WeeklyFoodRules?.Targets
                ?.Select(target => new WeeklyFoodTarget
                {
                    FoodGroup = target.FoodGroup,
                    RuleType = target.RuleType,
                    MealsPerWeek = target.MealsPerWeek
                })
                .ToList() ?? [],
                DayRules = settings?.WeeklyFoodRules?.DayRules
                    ?.Select(rule => new DayFoodRule
                    {
                        DayOfWeek = rule.DayOfWeek,
                        FoodGroup = rule.FoodGroup,
                        FoodGroups = rule.FoodGroups?.ToList() ?? []
                    })
                    .ToList() ?? []
            },
        CookingPreferences = new HouseholdCookingPreferences
        {
            MaximumWeekdayCookingMinutes = settings?.CookingPreferences?.MaximumWeekdayCookingMinutes ?? 45,
            MaximumWeekendCookingMinutes = settings?.CookingPreferences?.MaximumWeekendCookingMinutes ?? 90,
            UseFreezerMeals = settings?.CookingPreferences?.UseFreezerMeals ?? true,
            PreferSeasonalIngredients = settings?.CookingPreferences?.PreferSeasonalIngredients ?? true,
            PreferLocalIngredients = settings?.CookingPreferences?.PreferLocalIngredients ?? true,
            AvoidRepeatingRecipesWithinDays = settings?.CookingPreferences?.AvoidRepeatingRecipesWithinDays ?? 10,
            PreferFavouriteRecipes = settings?.CookingPreferences?.PreferFavouriteRecipes ?? true,
            PreferSuccessfullyCookedRecipes = settings?.CookingPreferences?.PreferSuccessfullyCookedRecipes ?? true,
            AllowLeftovers = settings?.CookingPreferences?.AllowLeftovers ?? true,
            MinimumVarietyMealsPerWeek = settings?.CookingPreferences?.MinimumVarietyMealsPerWeek ?? 7,
            DesiredVarietyWindowDays = settings?.CookingPreferences?.DesiredVarietyWindowDays ?? 14,
            PrioritizeAvailableFreezerIngredients = settings?.CookingPreferences?.PrioritizeAvailableFreezerIngredients ?? true,
            PrioritizePantryIngredients = settings?.CookingPreferences?.PrioritizePantryIngredients ?? true,
            PreferBatchFriendlyPreparations = settings?.CookingPreferences?.PreferBatchFriendlyPreparations ?? true
        },
        KitchenPantry = new HouseholdKitchenPantrySettings
        {
            AlwaysAvailableIngredients = settings?.KitchenPantry?.AlwaysAvailableIngredients ?? string.Empty,
            FridgeInventoryNotes = settings?.KitchenPantry?.FridgeInventoryNotes ?? string.Empty,
            FreezerInventoryNotes = settings?.KitchenPantry?.FreezerInventoryNotes ?? string.Empty,
            PantryStaplesNotes = settings?.KitchenPantry?.PantryStaplesNotes ?? string.Empty,
            PreferredBrands = settings?.KitchenPantry?.PreferredBrands ?? string.Empty,
            FreezerItems = settings?.KitchenPantry?.FreezerItems?
                .Select(item => new FreezerInventoryItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    FrozenDate = item.FrozenDate,
                    BestBeforeDate = item.BestBeforeDate,
                    Notes = item.Notes
                })
                .ToList() ?? [],
            Appliances = new KitchenApplianceSettings
            {
                AirFryer = settings?.KitchenPantry?.Appliances?.AirFryer ?? false,
                PressureCooker = settings?.KitchenPantry?.Appliances?.PressureCooker ?? false,
                Oven = settings?.KitchenPantry?.Appliances?.Oven ?? true,
                Bbq = settings?.KitchenPantry?.Appliances?.Bbq ?? false,
                Thermomix = settings?.KitchenPantry?.Appliances?.Thermomix ?? false,
                SteamCooker = settings?.KitchenPantry?.Appliances?.SteamCooker ?? false
            }
        },
        Shopping = new HouseholdShoppingSettings
        {
            MergeDuplicatedIngredients = settings?.Shopping?.MergeDuplicatedIngredients ?? true,
            SortBySupermarketOrder = settings?.Shopping?.SortBySupermarketOrder ?? true,
            IgnorePantryItems = settings?.Shopping?.IgnorePantryItems ?? true,
            IgnoreAlwaysAvailableIngredients = settings?.Shopping?.IgnoreAlwaysAvailableIngredients ?? true,
            DeductAvailableFreezerItems = settings?.Shopping?.DeductAvailableFreezerItems ?? false,
            AutomaticQuantityAggregation = settings?.Shopping?.AutomaticQuantityAggregation ?? true,
            PreferredUnits = settings?.Shopping?.PreferredUnits ?? PreferredUnitMode.RecipeUnits,
            DefaultFreshShoppingDay = settings?.Shopping?.DefaultFreshShoppingDay ?? DayOfWeek.Saturday,
            DefaultGeneralShoppingDay = settings?.Shopping?.DefaultGeneralShoppingDay ?? DayOfWeek.Saturday,
            PreserveManualItemsWhenRegenerating = settings?.Shopping?.PreserveManualItemsWhenRegenerating ?? true
        }
    };

    private static decimal CalculateMacroGrams(decimal calories, decimal percent, decimal caloriesPerGram) =>
        calories <= 0 || percent <= 0 || caloriesPerGram <= 0
            ? 0
            : Math.Round(calories * percent / 100m / caloriesPerGram, 1);
}
