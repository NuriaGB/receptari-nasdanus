namespace Nasdanus.KnowledgeImporter.Pipeline;

public sealed class KnowledgeValidationReport
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> UnknownCategories { get; set; } = [];
    public List<string> DuplicateAliases { get; set; } = [];
    public List<string> MissingNutrition { get; set; } = [];
    public List<string> MissingUnits { get; set; } = [];
    public List<string> DuplicateIngredients { get; set; } = [];

    public bool HasIssues =>
        UnknownCategories.Count > 0
        || DuplicateAliases.Count > 0
        || MissingNutrition.Count > 0
        || MissingUnits.Count > 0
        || DuplicateIngredients.Count > 0;
}
