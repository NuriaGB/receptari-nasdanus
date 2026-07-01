namespace Nasdanus.Domain;

public sealed class RecipeEditRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Servings { get; set; }
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Difficulty { get; set; }
    public List<RecipeIngredientEditRequest> Ingredients { get; set; } = [];
    public List<RecipeStepEditRequest> Steps { get; set; } = [];
    public List<RecipeNoteEditRequest> Notes { get; set; } = [];
}

public sealed class RecipeIngredientEditRequest
{
    public string Key { get; set; } = string.Empty;
    public int? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ScalingMode { get; set; } = IngredientScalingMode.Linear;
    public int Order { get; set; }
}

public sealed class RecipeStepEditRequest
{
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public int Order { get; set; }
    public List<RecipeStepIngredientEditRequest> IngredientReferences { get; set; } = [];
}

public sealed class RecipeStepIngredientEditRequest
{
    public string IngredientKey { get; set; } = string.Empty;
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class RecipeNoteEditRequest
{
    public string Section { get; set; } = RecipeNoteSection.General;
    public string Content { get; set; } = string.Empty;
    public int Order { get; set; }
}
