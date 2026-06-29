namespace Nasdanus.Domain;

public sealed class RecipeIdea
{
    public int Id { get; set; }
    public DateOnly WeekStart { get; set; }
    public int RecipeId { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
