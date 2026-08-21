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
    MealNutritionSummary Breakfast,
    MealNutritionSummary Lunch,
    MealNutritionSummary Dinner,
    NutritionTotals Totals);

public sealed record WeekNutritionSummary(
    DateOnly WeekStart,
    IReadOnlyList<DayNutritionSummary> Days,
    NutritionTotals Totals);

public sealed class HouseholdPlanningSettings
{
    public HouseholdGeneralSettings General { get; set; } = new();
    public List<HouseholdMemberProfile> Members { get; set; } = HouseholdMemberDefaults.Create();
    public HouseholdNutritionGoals NutritionGoals { get; set; } = new();
    public WeeklyFoodRules WeeklyFoodRules { get; set; } = new();
    public HouseholdCookingPreferences CookingPreferences { get; set; } = new();
    public HouseholdKitchenPantrySettings KitchenPantry { get; set; } = new();
    public HouseholdShoppingSettings Shopping { get; set; } = new();
}

public sealed class HouseholdGeneralSettings
{
    public string HouseholdName { get; set; } = "Nasdanus";
    public string DefaultLanguage { get; set; } = HouseholdLanguage.Catalan;
    public string MeasurementSystem { get; set; } = MeasurementSystemKind.Metric;
    public int DefaultServings { get; set; } = 4;
    public DayOfWeek WeekStartsOn { get; set; } = DayOfWeek.Monday;
}

public sealed class HouseholdMemberProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? MeasurementDate { get; set; }
    public decimal HeightCentimeters { get; set; }
    public decimal WeightKilograms { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public string Sex { get; set; } = MemberSex.Unspecified;
    public string CurrentLifeStage { get; set; } = string.Empty;
    public string ActivityLevel { get; set; } = MemberActivityLevel.Moderate;
    public string WeeklyExercise { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string HealthNotes { get; set; } = string.Empty;
    public string FavouriteFoods { get; set; } = string.Empty;
    public string FoodsToAvoid { get; set; } = string.Empty;
    public string FoodsToEncourage { get; set; } = string.Empty;
    public string SpiceTolerance { get; set; } = SpiceToleranceLevel.Medium;
    public string CookingPreferences { get; set; } = string.Empty;
    public List<string> NutritionGoals { get; set; } = [];
    public string CustomNutritionGoals { get; set; } = string.Empty;
    public List<BodyMeasurement> MeasurementHistory { get; set; } = [];
}

public sealed class BodyMeasurement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal WeightKilograms { get; set; }
    public decimal? HeightCentimeters { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public static class HouseholdMemberDefaults
{
    public static List<HouseholdMemberProfile> Create() =>
    [
        new HouseholdMemberProfile
        {
            Id = "nuria",
            Name = "Núria",
            DateOfBirth = new DateTime(1981, 5, 15),
            MeasurementDate = DateTime.Today,
            HeightCentimeters = 167,
            WeightKilograms = 91,
            Sex = MemberSex.Female,
            CurrentLifeStage = "Perimenopause ongoing for more than two years.",
            ActivityLevel = MemberActivityLevel.Active,
            WeeklyExercise = "Approximately 4-5 hours/week combining cardio, strength / toning and Pilates.",
            HealthNotes = "General considerations only; no medical advice.",
            FoodsToEncourage = "High-protein meals, balanced weekly choices.",
            SpiceTolerance = SpiceToleranceLevel.Medium,
            CookingPreferences = "Reduce mental load when planning meals.",
            NutritionGoals =
            [
                MemberNutritionGoal.IncreaseProtein,
                MemberNutritionGoal.ImproveOverallDiet
            ],
            CustomNutritionGoals = "Improve meal balance across the week and maintain a sustainable, healthy diet.",
            MeasurementHistory =
            [
                new BodyMeasurement
                {
                    Id = "nuria-initial",
                    Date = DateTime.Today,
                    WeightKilograms = 91,
                    HeightCentimeters = 167,
                    Notes = "Initial household profile measurement."
                }
            ]
        },
        new HouseholdMemberProfile
        {
            Id = "alex",
            Name = "Alex",
            DateOfBirth = new DateTime(1979, 1, 17),
            MeasurementDate = DateTime.Today,
            HeightCentimeters = 183,
            WeightKilograms = 105,
            Sex = MemberSex.Male,
            ActivityLevel = MemberActivityLevel.Active,
            WeeklyExercise = "Approximately four sessions/week: around 20 min elliptical and 20 min strength. Rotation includes chest, biceps, triceps, quadriceps, core and other muscle groups.",
            HealthNotes = "Hypertension treated with medication. Prefer moderate sodium and avoid unnecessarily high-salt meals.",
            FavouriteFoods = "Spicy food.",
            FoodsToAvoid = "Fish with bones; chicken thighs; mashed potatoes; rare meat.",
            FoodsToEncourage = "Moderate-sodium meals.",
            SpiceTolerance = SpiceToleranceLevel.High,
            NutritionGoals =
            [
                MemberNutritionGoal.ImproveOverallDiet,
                MemberNutritionGoal.IncreaseProtein
            ],
            CustomNutritionGoals = "Support training while keeping sodium moderate.",
            MeasurementHistory =
            [
                new BodyMeasurement
                {
                    Id = "alex-initial",
                    Date = DateTime.Today,
                    WeightKilograms = 105,
                    HeightCentimeters = 183,
                    Notes = "Initial household profile measurement."
                }
            ]
        },
        new HouseholdMemberProfile
        {
            Id = "cora",
            Name = "Cora",
            DateOfBirth = new DateTime(2014, 11, 21),
            MeasurementDate = DateTime.Today,
            HeightCentimeters = 156,
            WeightKilograms = 56.5m,
            Sex = MemberSex.Female,
            CurrentLifeStage = "Approaching puberty; dense/athletic body type.",
            ActivityLevel = MemberActivityLevel.Active,
            WeeklyExercise = "Judo 2-3 times/week and dance 1-2 times/week.",
            FavouriteFoods = "Pasta; homemade burgers.",
            FoodsToEncourage = "High-quality protein, omega-3, DHA, phosphorus, minerals and micronutrients important for cognitive development.",
            SpiceTolerance = SpiceToleranceLevel.Low,
            NutritionGoals =
            [
                MemberNutritionGoal.ImproveOverallDiet
            ],
            CustomNutritionGoals = "Support healthy growth, school, sport and development with enough daily energy.",
            MeasurementHistory =
            [
                new BodyMeasurement
                {
                    Id = "cora-initial",
                    Date = DateTime.Today,
                    WeightKilograms = 56.5m,
                    HeightCentimeters = 156,
                    Notes = "Initial household profile measurement."
                }
            ]
        }
    ];
}

public sealed class HouseholdNutritionGoals
{
    public string GoalScope { get; set; } = NutritionGoalScope.WeeklyAverage;
    public string MacroMode { get; set; } = NutritionMacroMode.PercentageDistribution;
    public decimal TargetCaloriesPerPerson { get; set; } = 2000;
    public decimal MinimumProteinGramsPerPerson { get; set; } = 150;
    public decimal TargetCarbohydrateGramsPerPerson { get; set; } = 175;
    public decimal TargetFatGramsPerPerson { get; set; } = 77.8m;
    public decimal TargetFibreGramsPerPerson { get; set; } = 30;
    public decimal ProteinPercent { get; set; } = 30;
    public decimal CarbohydratePercent { get; set; } = 35;
    public decimal FatPercent { get; set; } = 35;
}

public sealed class WeeklyFoodRules
{
    public List<DayFoodRule> DayRules { get; set; } = [];
    public List<WeeklyFoodTarget> Targets { get; set; } = [];
    public int MinimumFishMeals { get; set; } = 2;
    public int MinimumLegumeMeals { get; set; } = 1;
    public int MaximumRedMeatMeals { get; set; } = 1;
    public int MinimumVegetableRichMeals { get; set; } = 7;
}

public sealed class WeeklyFoodTarget
{
    public string FoodGroup { get; set; } = FoodGroupKind.None;
    public string RuleType { get; set; } = WeeklyFoodRuleType.Minimum;
    public int MealsPerWeek { get; set; }
}

public sealed class DayFoodRule
{
    public DayOfWeek DayOfWeek { get; set; }
    public string FoodGroup { get; set; } = FoodGroupKind.None;
    public List<string> FoodGroups { get; set; } = [];
}

public sealed class HouseholdCookingPreferences
{
    public int MaximumWeekdayCookingMinutes { get; set; } = 45;
    public int MaximumWeekendCookingMinutes { get; set; } = 90;
    public bool UseFreezerMeals { get; set; } = true;
    public bool PreferSeasonalIngredients { get; set; } = true;
    public bool PreferLocalIngredients { get; set; } = true;
    public int AvoidRepeatingRecipesWithinDays { get; set; } = 10;
    public bool PreferFavouriteRecipes { get; set; } = true;
    public bool PreferSuccessfullyCookedRecipes { get; set; } = true;
    public bool AllowLeftovers { get; set; } = true;
    public int MinimumVarietyMealsPerWeek { get; set; } = 7;
    public int DesiredVarietyWindowDays { get; set; } = 14;
    public bool PrioritizeAvailableFreezerIngredients { get; set; } = true;
    public bool PrioritizePantryIngredients { get; set; } = true;
    public bool PreferBatchFriendlyPreparations { get; set; } = true;
}

public sealed class HouseholdKitchenPantrySettings
{
    public string AlwaysAvailableIngredients { get; set; } = string.Empty;
    public string FridgeInventoryNotes { get; set; } = string.Empty;
    public string FreezerInventoryNotes { get; set; } = string.Empty;
    public string PantryStaplesNotes { get; set; } = string.Empty;
    public string PreferredBrands { get; set; } = string.Empty;
    public List<FreezerInventoryItem> FreezerItems { get; set; } = [];
    public KitchenApplianceSettings Appliances { get; set; } = new();
}

public sealed class FreezerInventoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime? FrozenDate { get; set; }
    public DateTime? BestBeforeDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class KitchenApplianceSettings
{
    public bool AirFryer { get; set; }
    public bool PressureCooker { get; set; }
    public bool Oven { get; set; } = true;
    public bool Bbq { get; set; }
    public bool Thermomix { get; set; }
    public bool SteamCooker { get; set; }
}

public sealed class HouseholdShoppingSettings
{
    public bool MergeDuplicatedIngredients { get; set; } = true;
    public bool SortBySupermarketOrder { get; set; } = true;
    public bool IgnorePantryItems { get; set; } = true;
    public bool IgnoreAlwaysAvailableIngredients { get; set; } = true;
    public bool DeductAvailableFreezerItems { get; set; } = false;
    public bool AutomaticQuantityAggregation { get; set; } = true;
    public string PreferredUnits { get; set; } = PreferredUnitMode.RecipeUnits;
    public DayOfWeek DefaultFreshShoppingDay { get; set; } = DayOfWeek.Saturday;
    public DayOfWeek DefaultGeneralShoppingDay { get; set; } = DayOfWeek.Saturday;
    public bool PreserveManualItemsWhenRegenerating { get; set; } = true;
}

public static class HouseholdLanguage
{
    public const string Catalan = "ca";
    public const string Spanish = "es";
    public const string English = "en";

    public static readonly string[] All = [Catalan, Spanish, English];

    public static string ToDisplayName(string language) => language switch
    {
        Catalan => "Catala",
        Spanish => "Castella",
        English => "Angles",
        _ => language
    };
}

public static class MeasurementSystemKind
{
    public const string Metric = "Metric";
    public const string Imperial = "Imperial";

    public static readonly string[] All = [Metric, Imperial];

    public static string ToDisplayName(string system) => system switch
    {
        Metric => "Metric",
        Imperial => "Imperial",
        _ => system
    };
}

public static class MemberSex
{
    public const string Female = "Female";
    public const string Male = "Male";
    public const string Other = "Other";
    public const string Unspecified = "Unspecified";

    public static readonly string[] All = [Female, Male, Other, Unspecified];

    public static string ToDisplayName(string sex) => sex switch
    {
        Female => "Female",
        Male => "Male",
        Other => "Other",
        Unspecified => "Sense especificar",
        _ => sex
    };
}

public static class MemberActivityLevel
{
    public const string Low = "Low";
    public const string Moderate = "Moderate";
    public const string Active = "Active";
    public const string VeryActive = "VeryActive";

    public static readonly string[] All = [Low, Moderate, Active, VeryActive];

    public static string ToDisplayName(string level) => level switch
    {
        Low => "Baixa",
        Moderate => "Moderada",
        Active => "Activa",
        VeryActive => "Molt activa",
        _ => level
    };
}

public static class SpiceToleranceLevel
{
    public const string None = "None";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    public static readonly string[] All = [None, Low, Medium, High];

    public static string ToDisplayName(string level) => level switch
    {
        None => "Gens",
        Low => "Baixa",
        Medium => "Mitjana",
        High => "Alta",
        _ => level
    };
}

public static class MemberNutritionGoal
{
    public const string MaintainWeight = "MaintainWeight";
    public const string LoseWeight = "LoseWeight";
    public const string GainMuscle = "GainMuscle";
    public const string IncreaseProtein = "IncreaseProtein";
    public const string ImproveOverallDiet = "ImproveOverallDiet";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        MaintainWeight,
        LoseWeight,
        GainMuscle,
        IncreaseProtein,
        ImproveOverallDiet,
        Other
    ];

    public static string ToDisplayName(string goal) => goal switch
    {
        MaintainWeight => "Mantenir pes",
        LoseWeight => "Perdre pes",
        GainMuscle => "Guanyar muscul",
        IncreaseProtein => "Augmentar proteina",
        ImproveOverallDiet => "Millorar dieta global",
        Other => "Altres",
        _ => goal
    };
}

public static class NutritionGoalScope
{
    public const string Daily = "Daily";
    public const string WeeklyAverage = "WeeklyAverage";
    public const string Meal = "Meal";

    public static readonly string[] All = [Daily, WeeklyAverage, Meal];

    public static string ToDisplayName(string scope) => scope switch
    {
        Daily => "Daily target",
        WeeklyAverage => "Weekly daily average",
        Meal => "Meal goals",
        _ => scope
    };
}

public static class NutritionMacroMode
{
    public const string AbsoluteGrams = "AbsoluteGrams";
    public const string PercentageDistribution = "PercentageDistribution";

    public static readonly string[] All = [AbsoluteGrams, PercentageDistribution];

    public static string ToDisplayName(string mode) => mode switch
    {
        AbsoluteGrams => "Grams per person per day",
        PercentageDistribution => "Percentage of calories",
        _ => mode
    };
}

public static class WeeklyFoodRuleType
{
    public const string Minimum = "Minimum";
    public const string Maximum = "Maximum";
    public const string Target = "Target";

    public static readonly string[] All = [Minimum, Maximum, Target];

    public static string ToDisplayName(string ruleType) => ruleType switch
    {
        Minimum => "Minimum meals/week",
        Maximum => "Maximum meals/week",
        Target => "Target meals/week",
        _ => ruleType
    };
}

public static class PreferredUnitMode
{
    public const string RecipeUnits = "RecipeUnits";
    public const string Metric = "Metric";
    public const string ShoppingFriendly = "ShoppingFriendly";

    public static readonly string[] All = [RecipeUnits, Metric, ShoppingFriendly];

    public static string ToDisplayName(string mode) => mode switch
    {
        RecipeUnits => "Unitats de la recepta",
        Metric => "Metric",
        ShoppingFriendly => "Unitats de compra",
        _ => mode
    };
}

public static class FoodGroupKind
{
    public const string None = "";
    public const string BlueFish = "BlueFish";
    public const string WhiteFish = "WhiteFish";
    public const string Fish = "Fish";
    public const string Seafood = "Seafood";
    public const string Legumes = "Legumes";
    public const string VegetableRich = "VegetableRich";
    public const string RedMeat = "RedMeat";
    public const string WhiteMeat = "WhiteMeat";
    public const string Poultry = "Poultry";
    public const string Chicken = "Chicken";
    public const string Meat = "Meat";
    public const string Eggs = "Eggs";
    public const string Pasta = "Pasta";
    public const string Rice = "Rice";
    public const string Vegetarian = "Vegetarian";
    public const string FastFood = "FastFood";
    public const string HomemadeFastFood = "HomemadeFastFood";
    public const string Desserts = "Desserts";
    public const string Vegetables = "Vegetables";
    public const string HighProtein = "HighProtein";

    public static readonly string[] PlanningGroups =
    [
        BlueFish,
        WhiteFish,
        Fish,
        Seafood,
        Legumes,
        VegetableRich,
        RedMeat,
        WhiteMeat,
        Poultry,
        Chicken,
        Meat,
        Eggs,
        Pasta,
        Rice,
        Vegetarian,
        FastFood,
        HomemadeFastFood,
        Desserts,
        Vegetables,
        HighProtein
    ];

    public static string ToDisplayName(string foodGroup) => foodGroup switch
    {
        BlueFish => "Peix blau",
        WhiteFish => "Peix blanc",
        Fish => "Peix",
        Seafood => "Marisc",
        Legumes => "Llegums",
        VegetableRich => "Ric en verdures",
        RedMeat => "Carn vermella",
        WhiteMeat => "Carn blanca",
        Poultry => "Aus",
        Chicken => "Pollastre",
        Meat => "Carn",
        Eggs => "Ous",
        Pasta => "Pasta",
        Rice => "Arros",
        Vegetarian => "Vegetaria",
        FastFood => "Fast food",
        HomemadeFastFood => "Fast food casola",
        Desserts => "Postres",
        Vegetables => "Verdures",
        HighProtein => "Alt en proteina",
        _ => "Sense regla"
    };
}

public sealed record RecipeFoodProfile(
    bool IsFish,
    bool IsBlueFish,
    bool IsWhiteFish,
    bool IsLegume,
    bool IsVegetableRich,
    bool IsRedMeat,
    bool IsChicken,
    bool IsMeat,
    bool HasEggs,
    bool IsPasta,
    bool IsRice,
    bool IsVegetarian,
    bool IsFastFood,
    bool IsDessert,
    int VegetableIngredientCount,
    string PrimaryGroup)
{
    public bool Matches(string foodGroup) => foodGroup switch
    {
        FoodGroupKind.BlueFish => IsBlueFish,
        FoodGroupKind.WhiteFish => IsWhiteFish,
        FoodGroupKind.Fish => IsFish,
        FoodGroupKind.Seafood => IsFish,
        FoodGroupKind.Legumes => IsLegume,
        FoodGroupKind.VegetableRich => IsVegetableRich,
        FoodGroupKind.RedMeat => IsRedMeat,
        FoodGroupKind.WhiteMeat => IsChicken,
        FoodGroupKind.Poultry => IsChicken,
        FoodGroupKind.Chicken => IsChicken,
        FoodGroupKind.Meat => IsMeat,
        FoodGroupKind.Eggs => HasEggs,
        FoodGroupKind.Pasta => IsPasta,
        FoodGroupKind.Rice => IsRice,
        FoodGroupKind.Vegetarian => IsVegetarian,
        FoodGroupKind.FastFood => IsFastFood,
        FoodGroupKind.HomemadeFastFood => IsFastFood,
        FoodGroupKind.Desserts => IsDessert,
        FoodGroupKind.Vegetables => VegetableIngredientCount > 0 || IsVegetableRich,
        FoodGroupKind.HighProtein => IsFish || IsMeat || HasEggs || IsLegume,
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
