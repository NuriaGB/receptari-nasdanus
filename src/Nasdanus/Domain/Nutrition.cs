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
    public int KnownIngredientCount { get; set; }
    public int UnknownNutritionCount { get; set; }
    public int UnknownQuantityCount { get; set; }

    public bool HasKnownNutrition => KnownIngredientCount > 0;

    public void Add(NutritionTotals other)
    {
        CaloriesKcal += other.CaloriesKcal;
        ProteinGrams += other.ProteinGrams;
        CarbohydrateGrams += other.CarbohydrateGrams;
        FatGrams += other.FatGrams;
        FibreGrams += other.FibreGrams;
        SugarGrams += other.SugarGrams;
        SaltGrams += other.SaltGrams;
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
        KnownIngredientCount = KnownIngredientCount,
        UnknownNutritionCount = UnknownNutritionCount,
        UnknownQuantityCount = UnknownQuantityCount
    };
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
