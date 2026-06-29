namespace Nasdanus.Domain;

public sealed class PantryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ShoppingCategory.Other;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PantryItemEditRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ShoppingCategory.Other;
}
