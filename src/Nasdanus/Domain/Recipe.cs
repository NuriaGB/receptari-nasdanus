using System.ComponentModel.DataAnnotations.Schema;

namespace Nasdanus.Domain;

public sealed class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = RecipeStatus.Active;
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Difficulty { get; set; }
    public int Servings { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];
    public List<RecipeStep> Steps { get; set; } = [];
    public List<RecipePlanningMetadata> PlanningMetadata { get; set; } = [];

    [NotMapped]
    public bool IsDraft => string.Equals(Status, RecipeStatus.Draft, StringComparison.OrdinalIgnoreCase);
}

public static class RecipeStatus
{
    public const string Active = "Active";
    public const string Draft = "Draft";
}

public sealed class RecipePlanningMetadata
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class RecipePlanningMetadataKind
{
    public const string WeeklyFavourite = "WeeklyFavourite";
    public const string Fortnightly = "Fortnightly";
    public const string Monthly = "Monthly";
    public const string Seasonal = "Seasonal";
    public const string SpecialOccasion = "SpecialOccasion";
}

public sealed class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ScalingMode { get; set; } = IngredientScalingMode.Linear;

    [NotMapped]
    public string DisplayText => ToDisplayText(scale: 1);

    public string ToDisplayText(decimal scale) => IngredientScaling.FormatIngredient(this, scale);
}

public static class IngredientScalingMode
{
    public const string Linear = "linear";
    public const string Fixed = "fixed";
    public const string Approximate = "approximate";
    public const string ToTaste = "to_taste";
}

public sealed class RecipeStep
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public List<RecipeStepIngredientReference> IngredientReferences { get; set; } = [];
}

public sealed class RecipeStepIngredientReference
{
    public int Id { get; set; }
    public int RecipeStepId { get; set; }
    public RecipeStep? Step { get; set; }
    public int? RecipeIngredientId { get; set; }
    public RecipeIngredient? Ingredient { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Order { get; set; }

    [NotMapped]
    public string DisplayText => ToDisplayText(scale: 1);

    public string ToDisplayText(decimal scale) => IngredientScaling.FormatStepIngredient(this, scale);
}
