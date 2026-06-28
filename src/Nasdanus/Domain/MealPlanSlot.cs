namespace Nasdanus.Domain;

public sealed class MealPlanSlot
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public MealKind MealKind { get; set; }
    public List<MealPlanRecipe> PlannedRecipes { get; set; } = [];
}

public sealed class MealPlanRecipe
{
    public int Id { get; set; }
    public int MealPlanSlotId { get; set; }
    public MealPlanSlot? MealPlanSlot { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int PlannedServings { get; set; }
    public int Order { get; set; }
}
