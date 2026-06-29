namespace Nasdanus.Domain;

public sealed class FreezerItem
{
    public int Id { get; set; }
    public string Ingredient { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public DateOnly? FrozenDate { get; set; }
    public DateOnly? BestBefore { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
