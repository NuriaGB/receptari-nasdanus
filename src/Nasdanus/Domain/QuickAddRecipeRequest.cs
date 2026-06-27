namespace Nasdanus.Domain;

public sealed class QuickAddRecipeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string IngredientsText { get; set; } = string.Empty;
    public string StepsText { get; set; } = string.Empty;
    public int? PreparationTimeMinutes { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public int? Servings { get; set; }
}
