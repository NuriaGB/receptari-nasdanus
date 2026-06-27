namespace Nasdanus.Domain;

public sealed class MealPlanSlot
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public MealKind MealKind { get; set; }
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
}
