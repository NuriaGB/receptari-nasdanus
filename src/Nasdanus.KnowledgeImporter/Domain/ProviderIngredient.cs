namespace Nasdanus.KnowledgeImporter.Domain;

public sealed class ProviderIngredient
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string Category { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public bool? CanFreeze { get; set; }
    public string PantryCategory { get; set; } = string.Empty;
    public NutritionFacts? Nutrition { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProviderIngredientSearchResult
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProviderExportResult
{
    public string Provider { get; set; } = string.Empty;
    public IReadOnlyList<ProviderIngredient> Ingredients { get; init; } = [];
    public IReadOnlyList<CanonicalProduct> Products { get; init; } = [];
}
