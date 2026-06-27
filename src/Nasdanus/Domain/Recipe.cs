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

    [NotMapped]
    public bool IsDraft => string.Equals(Status, RecipeStatus.Draft, StringComparison.OrdinalIgnoreCase);
}

public static class RecipeStatus
{
    public const string Active = "Active";
    public const string Draft = "Draft";
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

    [NotMapped]
    public string DisplayText
    {
        get
        {
            var amount = string.Join(" ", new[] { Quantity, Unit }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(amount) ? Name : $"{amount} {Name}";
        }
    }
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
}
