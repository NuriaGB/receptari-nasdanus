namespace Nasdanus.Domain;

public static class AppInfo
{
    public const string Version = "v0.5.0";
}

public sealed class ProductBacklogItem
{
    public int Id { get; set; }
    public string Type { get; set; } = ProductBacklogType.Idea;
    public string Scope { get; set; } = ProductBacklogScope.General;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = ProductBacklogPriority.Medium;
    public string Status { get; set; } = ProductBacklogStatus.New;
    public int? DuplicateOfId { get; set; }
    public List<string> Labels { get; set; } = [];
    public string ApplicationVersion { get; set; } = AppInfo.Version;
    public string TargetVersion { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string ResolutionNotes { get; set; } = string.Empty;
    public ProductBacklogContext Context { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

public sealed class ProductBacklogContext
{
    public int? FeedbackId { get; set; }
    public string Page { get; set; } = string.Empty;
    public string CurrentUrl { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string BrowserInformation { get; set; } = string.Empty;
    public int? RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DateOnly? PlannerWeek { get; set; }
    public DateOnly? PlannerDay { get; set; }
    public string Meal { get; set; } = string.Empty;
    public int? CookingStepNumber { get; set; }
    public DateOnly? ShoppingWeek { get; set; }
    public string ShoppingCategory { get; set; } = string.Empty;
    public int? PantryItemId { get; set; }
    public string PantryItemName { get; set; } = string.Empty;
}

public sealed class ProductBacklogEditRequest
{
    public string Type { get; set; } = ProductBacklogType.Idea;
    public string Scope { get; set; } = ProductBacklogScope.General;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = ProductBacklogPriority.Medium;
    public string Status { get; set; } = ProductBacklogStatus.New;
    public int? DuplicateOfId { get; set; }
    public List<string> Labels { get; set; } = [];
    public string TargetVersion { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string ResolutionNotes { get; set; } = string.Empty;
}

public static class ProductBacklogType
{
    public const string Bug = "Bug";
    public const string Improvement = "Improvement";
    public const string Idea = "Idea";
    public const string Question = "Question";
    public const string Task = "Task";

    public static readonly string[] All =
    [
        Bug,
        Improvement,
        Idea,
        Question,
        Task
    ];

    public static string ToDisplayName(string type) => type switch
    {
        Bug => "Bug",
        Improvement => "Improvement",
        Question => "Question",
        Task => "Task",
        _ => "Idea"
    };

    public static string ToIcon(string type) => type switch
    {
        Bug => "🐞",
        Improvement => "✨",
        Question => "❓",
        Task => "📋",
        _ => "💡"
    };
}

public static class ProductBacklogScope
{
    public const string Home = "Home";
    public const string Planner = "Planner";
    public const string RecipeList = "Recipe List";
    public const string RecipeDetails = "Recipe Details";
    public const string RecipeEditor = "Recipe Editor";
    public const string CookingMode = "Cooking Mode";
    public const string ShoppingList = "Shopping List";
    public const string Pantry = "Pantry";
    public const string Freezer = "Freezer";
    public const string RecipeIdeas = "Recipe Ideas";
    public const string IngredientKnowledge = "Ingredient Knowledge";
    public const string PreparationKnowledge = "Preparation Knowledge";
    public const string Nutrition = "Nutrition";
    public const string Reports = "Reports";
    public const string Settings = "Settings";
    public const string General = "General";

    public static readonly string[] All =
    [
        Home,
        Planner,
        RecipeList,
        RecipeDetails,
        RecipeEditor,
        CookingMode,
        ShoppingList,
        Pantry,
        Freezer,
        RecipeIdeas,
        IngredientKnowledge,
        PreparationKnowledge,
        Nutrition,
        Reports,
        Settings,
        General
    ];
}

public static class ProductBacklogPriority
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly string[] All =
    [
        Low,
        Medium,
        High,
        Critical
    ];
}

public static class ProductBacklogStatus
{
    public const string New = "New";
    public const string InProgress = "In Progress";
    public const string Waiting = "Waiting";
    public const string Completed = "Completed";
    public const string WontFix = "Won't Fix";

    public static readonly string[] All =
    [
        New,
        InProgress,
        Waiting,
        Completed,
        WontFix
    ];

    public static bool IsClosed(string status) =>
        string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, WontFix, StringComparison.OrdinalIgnoreCase);
}

public static class ProductBacklogDecision
{
    public const string Implemented = "Implemented";
    public const string WontImplement = "Won't Implement";
    public const string Postponed = "Postponed";
    public const string Duplicate = "Duplicate";
    public const string Replaced = "Replaced";
    public const string CannotReproduce = "Cannot Reproduce";

    public static readonly string[] All =
    [
        Implemented,
        WontImplement,
        Postponed,
        Duplicate,
        Replaced,
        CannotReproduce
    ];
}

public static class ProductBacklogLabel
{
    public static readonly string[] Suggestions =
    [
        "UX",
        "Mobile",
        "Desktop",
        "Accessibility",
        "Performance",
        "Architecture",
        "Planner",
        "Shopping",
        "Cooking",
        "Import",
        "Data Model"
    ];
}
