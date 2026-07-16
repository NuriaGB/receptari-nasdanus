namespace Nasdanus.Domain;

public sealed class NutritionTotals
{
    public decimal CaloriesKcal { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbohydrateGrams { get; set; }
    public decimal FatGrams { get; set; }
    public decimal FibreGrams { get; set; }
    public decimal SugarGrams { get; set; }
    public decimal SaltGrams { get; set; }
    public int TotalIngredientCount { get; set; }
    public int LinkedIngredientCount { get; set; }
    public int KnownIngredientCount { get; set; }
    public int UnknownNutritionCount { get; set; }
    public int UnknownQuantityCount { get; set; }

    public bool HasKnownNutrition => KnownIngredientCount > 0;
    public bool HasMissingData => UnknownNutritionCount > 0 || UnknownQuantityCount > 0;
    public decimal ResolvedIngredientPercent => TotalIngredientCount == 0
        ? 0
        : LinkedIngredientCount * 100m / TotalIngredientCount;
    public int MissingNutritionDataCount => UnknownNutritionCount + UnknownQuantityCount;

    public void Add(NutritionTotals other)
    {
        CaloriesKcal += other.CaloriesKcal;
        ProteinGrams += other.ProteinGrams;
        CarbohydrateGrams += other.CarbohydrateGrams;
        FatGrams += other.FatGrams;
        FibreGrams += other.FibreGrams;
        SugarGrams += other.SugarGrams;
        SaltGrams += other.SaltGrams;
        TotalIngredientCount += other.TotalIngredientCount;
        LinkedIngredientCount += other.LinkedIngredientCount;
        KnownIngredientCount += other.KnownIngredientCount;
        UnknownNutritionCount += other.UnknownNutritionCount;
        UnknownQuantityCount += other.UnknownQuantityCount;
    }

    public NutritionTotals Clone() => new()
    {
        CaloriesKcal = CaloriesKcal,
        ProteinGrams = ProteinGrams,
        CarbohydrateGrams = CarbohydrateGrams,
        FatGrams = FatGrams,
        FibreGrams = FibreGrams,
        SugarGrams = SugarGrams,
        SaltGrams = SaltGrams,
        TotalIngredientCount = TotalIngredientCount,
        LinkedIngredientCount = LinkedIngredientCount,
        KnownIngredientCount = KnownIngredientCount,
        UnknownNutritionCount = UnknownNutritionCount,
        UnknownQuantityCount = UnknownQuantityCount
    };

    public NutritionTotals DivideBy(decimal divisor)
    {
        if (divisor <= 0)
        {
            return Clone();
        }

        return new NutritionTotals
        {
            CaloriesKcal = CaloriesKcal / divisor,
            ProteinGrams = ProteinGrams / divisor,
            CarbohydrateGrams = CarbohydrateGrams / divisor,
            FatGrams = FatGrams / divisor,
            FibreGrams = FibreGrams / divisor,
            SugarGrams = SugarGrams / divisor,
            SaltGrams = SaltGrams / divisor,
            TotalIngredientCount = TotalIngredientCount,
            LinkedIngredientCount = LinkedIngredientCount,
            KnownIngredientCount = KnownIngredientCount,
            UnknownNutritionCount = UnknownNutritionCount,
            UnknownQuantityCount = UnknownQuantityCount
        };
    }
}

public sealed record RecipeNutritionSummary(
    int RecipeId,
    string RecipeName,
    int Servings,
    NutritionTotals Totals);

public sealed record PlannedRecipeNutritionSummary(
    int PlannedRecipeId,
    int RecipeId,
    string RecipeName,
    int PlannedServings,
    NutritionTotals Totals);

public sealed record MealNutritionSummary(
    DateOnly Date,
    MealKind MealKind,
    IReadOnlyList<PlannedRecipeNutritionSummary> Recipes,
    NutritionTotals Totals);

public sealed record DayNutritionSummary(
    DateOnly Date,
    MealNutritionSummary Lunch,
    MealNutritionSummary Dinner,
    NutritionTotals Totals);

public sealed record WeekNutritionSummary(
    DateOnly WeekStart,
    IReadOnlyList<DayNutritionSummary> Days,
    NutritionTotals Totals);

public sealed class HouseholdPlanningSettings
{
    public HouseholdNutritionGoals NutritionGoals { get; set; } = new();
    public WeeklyFoodRules WeeklyFoodRules { get; set; } = new();
}

public sealed class HouseholdNutritionGoals
{
    public decimal TargetCaloriesPerPerson { get; set; } = 2000;
    public decimal MinimumProteinGramsPerPerson { get; set; } = 85;
    public decimal TargetCarbohydrateGramsPerPerson { get; set; } = 240;
    public decimal TargetFatGramsPerPerson { get; set; } = 70;
}

public sealed class WeeklyFoodRules
{
    public List<DayFoodRule> DayRules { get; set; } = [];
    public int MinimumFishMeals { get; set; } = 2;
    public int MinimumLegumeMeals { get; set; } = 1;
    public int MaximumRedMeatMeals { get; set; } = 1;
    public int MinimumVegetableRichMeals { get; set; } = 7;
}

public sealed class DayFoodRule
{
    public DayOfWeek DayOfWeek { get; set; }
    public string FoodGroup { get; set; } = FoodGroupKind.None;
}

public static class FoodGroupKind
{
    public const string None = "";
    public const string Fish = "Fish";
    public const string Legumes = "Legumes";
    public const string VegetableRich = "VegetableRich";
    public const string RedMeat = "RedMeat";
    public const string Chicken = "Chicken";
    public const string Meat = "Meat";
    public const string Eggs = "Eggs";
    public const string Pasta = "Pasta";
    public const string Vegetables = "Vegetables";

    public static readonly string[] PlanningGroups =
    [
        Fish,
        Legumes,
        VegetableRich,
        RedMeat,
        Chicken,
        Meat,
        Eggs,
        Pasta,
        Vegetables
    ];

    public static string ToDisplayName(string foodGroup) => foodGroup switch
    {
        Fish => "Fish",
        Legumes => "Legumes",
        VegetableRich => "Vegetable-rich",
        RedMeat => "Red meat",
        Chicken => "Chicken",
        Meat => "Meat",
        Eggs => "Eggs",
        Pasta => "Pasta",
        Vegetables => "Vegetables",
        _ => "No rule"
    };
}

public sealed record RecipeFoodProfile(
    bool IsFish,
    bool IsLegume,
    bool IsVegetableRich,
    bool IsRedMeat,
    bool IsChicken,
    bool IsMeat,
    bool HasEggs,
    bool IsPasta,
    int VegetableIngredientCount,
    string PrimaryGroup)
{
    public bool Matches(string foodGroup) => foodGroup switch
    {
        FoodGroupKind.Fish => IsFish,
        FoodGroupKind.Legumes => IsLegume,
        FoodGroupKind.VegetableRich => IsVegetableRich,
        FoodGroupKind.RedMeat => IsRedMeat,
        FoodGroupKind.Chicken => IsChicken,
        FoodGroupKind.Meat => IsMeat,
        FoodGroupKind.Eggs => HasEggs,
        FoodGroupKind.Pasta => IsPasta,
        FoodGroupKind.Vegetables => VegetableIngredientCount > 0 || IsVegetableRich,
        _ => false
    };
}

public sealed record RecipeSuggestion(
    Recipe Recipe,
    decimal Score,
    IReadOnlyList<string> Reasons,
    NutritionTotals NutritionPerServing,
    RecipeFoodProfile FoodProfile,
    bool IsAlreadyPlannedThisWeek,
    bool IsHighProtein,
    bool IsQuickMeal);

public sealed record FoodGroupTargetStatus(
    string FoodGroup,
    int Current,
    int Target,
    bool IsMaximum);

public sealed record WeeklyNutritionGoalStatus(
    WeekNutritionSummary Week,
    NutritionTotals AveragePerPersonPerDay,
    HouseholdPlanningSettings Settings,
    IReadOnlyList<FoodGroupTargetStatus> FoodGroupTargets);

public sealed record IngredientNutritionCandidate(
    string ProviderId,
    string ProviderName,
    string IngredientName,
    string Category,
    string DefaultUnit,
    IngredientNutrition NutritionPer100Grams,
    string SourceReference);

public interface IIngredientNutritionProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<IReadOnlyList<IngredientNutritionCandidate>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public interface IIngredientNutritionImportService
{
    Task<IReadOnlyList<IngredientNutritionCandidate>> SearchProvidersAsync(string query, CancellationToken cancellationToken = default);
}
