using System.Text.Json;
using System.Text.Json.Serialization;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class RecipeExchangeService(BrowserAppStore store)
{
    private const string ApplicationName = "Nasdanus";
    private const string FormatName = "recipe-card";
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public async Task<string> ExportAllAsync()
    {
        var state = await store.GetStateAsync();
        var recipes = state.Recipes
            .OrderBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .Select(store.CloneRecipe)
            .ToList();

        return SerializeFile(recipes);
    }

    public async Task<string?> ExportRecipeAsync(int id)
    {
        var state = await store.GetStateAsync();
        var recipe = store.FindRecipe(state, id);
        return recipe is null
            ? null
            : SerializeFile([store.CloneRecipe(recipe)]);
    }

    public string CreateTemplateJson()
    {
        var file = CreateFile([
            new RecipeCard
            {
                Name = string.Empty,
                Description = string.Empty,
                Categories = [RecipeCategory.Dinner],
                Status = RecipeStatus.Draft,
                Servings = 4,
                PreparationTimeMinutes = 10,
                CookingTimeMinutes = 25,
                Difficulty = 2,
                IsFavourite = false,
                Rating = null,
                SeasonalRecommendation = string.Empty,
                ImageUrl = string.Empty,
                Tags = ["sopar", RecipeTagConventions.EquipmentTag("forn")],
                Ingredients =
                [
                    new RecipeCardIngredient
                    {
                        Key = "ingredient-1",
                        Name = "Nom de l'ingredient",
                        Quantity = "200",
                        Unit = "g",
                        ScalingMode = IngredientScalingMode.Linear
                    }
                ],
                Steps =
                [
                    new RecipeCardStep
                    {
                        Title = "Pas 1",
                        Instruction = "Explica que cal fer.",
                        TimerMinutes = null,
                        IngredientReferences =
                        [
                            new RecipeCardStepIngredient
                            {
                                IngredientKey = "ingredient-1",
                                IngredientName = "Nom de l'ingredient",
                                QuantityText = "200",
                                Unit = "g"
                            }
                        ]
                    }
                ],
                Notes =
                [
                    new RecipeCardNote
                    {
                        Section = RecipeNoteSection.General,
                        Content = "Notes generals, trucs o substitucions."
                    }
                ],
                PlanningMetadata =
                [
                    new RecipeCardPlanningMetadata
                    {
                        Kind = RecipePlanningMetadataKind.Monthly,
                        Value = string.Empty,
                        Notes = "Quan interessa planificar-la."
                    }
                ],
                CookingHistory = []
            }
        ]);

        file.Template = true;
        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public Task<RecipeImportValidationResult> ValidateImportJsonAsync(string json)
    {
        var errors = new List<string>();
        var cards = ParseCards(json, errors);
        if (cards.Count == 0 && errors.Count == 0)
        {
            errors.Add("El fitxer no conte cap recepta per importar.");
        }

        if (errors.Count == 0)
        {
            errors.AddRange(ValidateCards(cards));
        }

        return Task.FromResult(errors.Count == 0
            ? RecipeImportValidationResult.Valid(cards, RecipeExchangeSummary.FromCards(cards))
            : RecipeImportValidationResult.Invalid(errors));
    }

    public async Task<RecipeImportResult> ImportAsync(string json)
    {
        var validation = await ValidateImportJsonAsync(json);
        if (!validation.IsValid)
        {
            return RecipeImportResult.Invalid(validation.Errors);
        }

        var state = await store.GetStateAsync();
        var existingNames = state.Recipes
            .Select(recipe => recipe.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedIds = new List<int>();
        var importedNames = new List<string>();

        foreach (var card in validation.Cards)
        {
            var recipe = CreateRecipe(card, state, existingNames);
            state.Recipes.Add(recipe);
            importedIds.Add(recipe.Id);
            importedNames.Add(recipe.Name);
        }

        await store.SaveAsync();

        return RecipeImportResult.Valid(
            RecipeExchangeSummary.FromRecipes(state.Recipes.Where(recipe => importedIds.Contains(recipe.Id))),
            importedIds,
            importedNames);
    }

    private static string SerializeFile(IReadOnlyList<Recipe> recipes) =>
        JsonSerializer.Serialize(CreateFile(recipes.Select(ToCard).ToList()), JsonOptions);

    private static RecipeCardFile CreateFile(IReadOnlyList<RecipeCard> cards) => new()
    {
        Application = ApplicationName,
        Format = FormatName,
        FormatVersion = FormatVersion,
        ExportedAt = DateTime.UtcNow,
        RequiredFields = ["recipes[].name"],
        FieldsForCompleteRecipe =
        [
            "recipes[].categories",
            "recipes[].servings",
            "recipes[].preparationTimeMinutes",
            "recipes[].cookingTimeMinutes",
            "recipes[].ingredients[].name",
            "recipes[].steps[].instruction"
        ],
        AcceptedValues = RecipeCardAcceptedValues.Create(),
        Recipes = cards.ToList()
    };

    private static List<RecipeCard> ParseCards(string json, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("El fitxer esta buit.");
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add($"El JSON no es valid: {ex.Message}");
            return [];
        }

        using (document)
        {
            var root = document.RootElement;
            if (TryGetProperty(root, "data", out _))
            {
                return ParseBackup(json, errors);
            }

            if (TryGetProperty(root, "schemaVersion", out _) && TryGetProperty(root, "recipes", out _))
            {
                return ParseRawState(json, errors);
            }

            if (TryGetProperty(root, "recipes", out _))
            {
                return ParseRecipeCardFile(json, errors);
            }

            if (TryGetProperty(root, "name", out _))
            {
                return ParseSingleRecipeCard(json, errors);
            }
        }

        errors.Add("El fitxer no sembla una fitxa de recepta de Nasdanus.");
        return [];
    }

    private static List<RecipeCard> ParseRecipeCardFile(string json, List<string> errors)
    {
        try
        {
            var file = JsonSerializer.Deserialize<RecipeCardFile>(json, JsonOptions);
            if (file?.Recipes is not { Count: > 0 })
            {
                errors.Add("El paquet no conte cap recepta.");
                return [];
            }

            if (!string.IsNullOrWhiteSpace(file.Format)
                && !string.Equals(file.Format, FormatName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"El format '{file.Format}' no es compatible amb les fitxes de recepta.");
            }

            return file.Recipes;
        }
        catch (JsonException ex)
        {
            errors.Add($"No s'ha pogut llegir el paquet de receptes: {ex.Message}");
            return [];
        }
    }

    private static List<RecipeCard> ParseBackup(string json, List<string> errors)
    {
        try
        {
            var backup = JsonSerializer.Deserialize<NasdanusBackupFile>(json, JsonOptions);
            if (backup?.Data?.Recipes is not { Count: > 0 })
            {
                errors.Add("La copia no conte cap recepta.");
                return [];
            }

            return backup.Data.Recipes.Select(ToCard).ToList();
        }
        catch (JsonException ex)
        {
            errors.Add($"No s'ha pogut llegir la copia de Nasdanus: {ex.Message}");
            return [];
        }
    }

    private static List<RecipeCard> ParseRawState(string json, List<string> errors)
    {
        try
        {
            var state = JsonSerializer.Deserialize<LocalAppState>(json, JsonOptions);
            if (state?.Recipes is not { Count: > 0 })
            {
                errors.Add("Les dades locals no contenen cap recepta.");
                return [];
            }

            return state.Recipes.Select(ToCard).ToList();
        }
        catch (JsonException ex)
        {
            errors.Add($"No s'han pogut llegir les dades locals: {ex.Message}");
            return [];
        }
    }

    private static List<RecipeCard> ParseSingleRecipeCard(string json, List<string> errors)
    {
        try
        {
            var card = JsonSerializer.Deserialize<RecipeCard>(json, JsonOptions);
            return card is null ? [] : [card];
        }
        catch (JsonException ex)
        {
            errors.Add($"No s'ha pogut llegir la fitxa de recepta: {ex.Message}");
            return [];
        }
    }

    private static IEnumerable<string> ValidateCards(IReadOnlyList<RecipeCard> cards)
    {
        var errors = new List<string>();
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var label = string.IsNullOrWhiteSpace(card.Name)
                ? $"recepta {index + 1}"
                : $"'{card.Name.Trim()}'";

            if (string.IsNullOrWhiteSpace(card.Name))
            {
                errors.Add($"La {label} no te nom.");
            }

            if (!string.IsNullOrWhiteSpace(card.Status) && !IsKnownStatus(card.Status))
            {
                errors.Add($"La recepta {label} te un status no acceptat: {card.Status}.");
            }

            if (card.Servings < 0)
            {
                errors.Add($"La recepta {label} te racions negatives.");
            }

            if (card.PreparationTimeMinutes < 0 || card.CookingTimeMinutes < 0)
            {
                errors.Add($"La recepta {label} te temps negatius.");
            }

            if (card.Difficulty is < 0 or > 5)
            {
                errors.Add($"La recepta {label} te una dificultat fora de rang (0..5).");
            }

            if (card.Rating is < 0 or > 5)
            {
                errors.Add($"La recepta {label} te un rating fora de rang (0..5).");
            }

            foreach (var ingredient in card.Ingredients.Where(ingredient => !IsBlank(ingredient)))
            {
                if (string.IsNullOrWhiteSpace(ingredient.Name))
                {
                    errors.Add($"La recepta {label} conte un ingredient sense nom.");
                }

                if (!IsKnownScalingMode(ingredient.ScalingMode))
                {
                    errors.Add($"La recepta {label} conte un mode d'escala no acceptat: {ingredient.ScalingMode}.");
                }
            }

            foreach (var step in card.Steps.Where(step => !IsBlank(step)))
            {
                if (step.TimerMinutes is < 0)
                {
                    errors.Add($"La recepta {label} conte un timer negatiu.");
                }
            }

            foreach (var note in card.Notes.Where(note => !string.IsNullOrWhiteSpace(note.Content)))
            {
                if (!RecipeNoteSection.DisplayOrder.Contains(note.Section))
                {
                    errors.Add($"La recepta {label} conte una seccio de notes no acceptada: {note.Section}.");
                }
            }

            foreach (var metadata in card.PlanningMetadata.Where(metadata => !IsBlank(metadata)))
            {
                if (!IsKnownPlanningKind(metadata.Kind))
                {
                    errors.Add($"La recepta {label} conte una frequencia de planning no acceptada: {metadata.Kind}.");
                }
            }
        }

        return errors.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private Recipe CreateRecipe(RecipeCard card, LocalAppState state, HashSet<string> existingNames)
    {
        var recipe = new Recipe
        {
            Id = store.NextId(state),
            Name = UniqueName(card.Name, existingNames),
            Description = card.Description.Trim(),
            Category = RecipeCategory.Format(card.Categories.Count > 0
                ? card.Categories
                : RecipeCategory.Parse(card.Category)),
            Status = NormalizeStatus(card.Status),
            PreparationTimeMinutes = Math.Max(0, card.PreparationTimeMinutes),
            CookingTimeMinutes = Math.Max(0, card.CookingTimeMinutes),
            Difficulty = Math.Clamp(card.Difficulty, 0, 5),
            Servings = Math.Max(0, card.Servings),
            IsFavourite = card.IsFavourite,
            Rating = card.Rating is int rating ? Math.Clamp(rating, 0, 5) : null,
            SeasonalRecommendation = card.SeasonalRecommendation.Trim(),
            ImageUrl = card.ImageUrl.Trim()
        };

        var ingredientsByKey = new Dictionary<string, RecipeIngredient>(StringComparer.OrdinalIgnoreCase);
        var ingredientsByName = new Dictionary<string, RecipeIngredient>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in card.Ingredients
            .Where(ingredient => !IsBlank(ingredient))
            .Select((ingredient, index) => (Ingredient: ingredient, Order: index + 1)))
        {
            var ingredient = new RecipeIngredient
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Order = item.Order,
                Name = item.Ingredient.Name.Trim(),
                Quantity = item.Ingredient.Quantity.Trim(),
                Unit = IngredientUnits.Normalize(item.Ingredient.Unit),
                ScalingMode = NormalizeScalingMode(item.Ingredient.ScalingMode)
            };

            recipe.Ingredients.Add(ingredient);
            var key = string.IsNullOrWhiteSpace(item.Ingredient.Key)
                ? IngredientKey(ingredient.Name, item.Order)
                : item.Ingredient.Key.Trim();
            ingredientsByKey[key] = ingredient;

            var normalizedName = FoodText.Normalize(ingredient.Name);
            if (!ingredientsByName.ContainsKey(normalizedName))
            {
                ingredientsByName[normalizedName] = ingredient;
            }
        }

        foreach (var item in card.Steps
            .Where(step => !IsBlank(step))
            .Select((step, index) => (Step: step, Order: index + 1)))
        {
            var step = new RecipeStep
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Order = item.Order,
                Title = item.Step.Title.Trim(),
                Instruction = item.Step.Instruction.Trim(),
                TimerMinutes = item.Step.TimerMinutes
            };

            foreach (var referenceItem in item.Step.IngredientReferences
                .Where(reference => !IsBlank(reference))
                .Select((reference, index) => (Reference: reference, Order: index + 1)))
            {
                var ingredient = ResolveStepIngredient(referenceItem.Reference, ingredientsByKey, ingredientsByName);
                step.IngredientReferences.Add(new RecipeStepIngredientReference
                {
                    Id = store.NextId(state),
                    RecipeStepId = step.Id,
                    RecipeIngredientId = ingredient?.Id,
                    Ingredient = ingredient,
                    IngredientName = ingredient?.Name ?? referenceItem.Reference.IngredientName.Trim(),
                    Quantity = IngredientScaling.ParseQuantity(referenceItem.Reference.QuantityText),
                    QuantityText = referenceItem.Reference.QuantityText.Trim(),
                    Unit = IngredientUnits.Normalize(referenceItem.Reference.Unit),
                    Order = referenceItem.Order
                });
            }

            recipe.Steps.Add(step);
        }

        recipe.Notes = card.Notes
            .Where(note => !string.IsNullOrWhiteSpace(note.Content))
            .Select((note, index) => new RecipeNote
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Section = RecipeNoteSection.DisplayOrder.Contains(note.Section) ? note.Section : RecipeNoteSection.General,
                Content = note.Content.Trim(),
                Order = index + 1,
                CreatedAt = note.CreatedAt ?? DateTime.UtcNow
            })
            .ToList();

        recipe.PlanningMetadata = card.PlanningMetadata
            .Where(metadata => !IsBlank(metadata))
            .Select(metadata => new RecipePlanningMetadata
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Kind = NormalizePlanningKind(metadata.Kind),
                Value = metadata.Value.Trim(),
                Notes = metadata.Notes.Trim(),
                CreatedAt = metadata.CreatedAt ?? DateTime.UtcNow
            })
            .ToList();

        recipe.Tags = card.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new RecipeTag
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Name = tag
            })
            .ToList();

        recipe.CookingHistory = card.CookingHistory
            .Select(session => new RecipeCookingSession
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                CookedAt = session.CookedAt == default ? DateTime.UtcNow : session.CookedAt,
                PlannedServings = session.PlannedServings,
                ActualServings = session.ActualServings,
                Rating = session.Rating,
                Notes = session.Notes.Trim()
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(card.Status))
        {
            recipe.Status = IsIncomplete(recipe) ? RecipeStatus.Draft : RecipeStatus.Active;
        }

        return recipe;
    }

    private static RecipeIngredient? ResolveStepIngredient(
        RecipeCardStepIngredient reference,
        Dictionary<string, RecipeIngredient> ingredientsByKey,
        Dictionary<string, RecipeIngredient> ingredientsByName)
    {
        if (!string.IsNullOrWhiteSpace(reference.IngredientKey)
            && ingredientsByKey.TryGetValue(reference.IngredientKey.Trim(), out var byKey))
        {
            return byKey;
        }

        var normalizedName = FoodText.Normalize(reference.IngredientName);
        return !string.IsNullOrWhiteSpace(normalizedName) && ingredientsByName.TryGetValue(normalizedName, out var byName)
            ? byName
            : null;
    }

    private static RecipeCard ToCard(Recipe recipe)
    {
        var ingredientKeys = recipe.Ingredients
            .OrderBy(ingredient => ingredient.Order)
            .Select((ingredient, index) => (Ingredient: ingredient, Key: IngredientKey(ingredient.Name, index + 1)))
            .ToDictionary(item => item.Ingredient.Id, item => item.Key);

        return new RecipeCard
        {
            SourceId = recipe.Id,
            Name = recipe.Name,
            Description = recipe.Description,
            Category = recipe.Category,
            Categories = RecipeCategory.Parse(recipe.Category).ToList(),
            Status = recipe.Status,
            PreparationTimeMinutes = recipe.PreparationTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Difficulty = recipe.Difficulty,
            Servings = recipe.Servings,
            IsFavourite = recipe.IsFavourite,
            Rating = recipe.Rating,
            SeasonalRecommendation = recipe.SeasonalRecommendation,
            ImageUrl = recipe.ImageUrl,
            Tags = recipe.Tags
                .Select(tag => tag.Name)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Ingredients = recipe.Ingredients
                .OrderBy(ingredient => ingredient.Order)
                .Select((ingredient, index) => new RecipeCardIngredient
                {
                    Key = ingredientKeys[ingredient.Id],
                    Name = ingredient.Name,
                    Quantity = ingredient.Quantity,
                    Unit = ingredient.Unit,
                    ScalingMode = ingredient.ScalingMode,
                    KnownIngredientName = ingredient.Ingredient?.Name
                })
                .ToList(),
            Steps = recipe.Steps
                .OrderBy(step => step.Order)
                .Select(step => new RecipeCardStep
                {
                    Title = step.Title,
                    Instruction = step.Instruction,
                    TimerMinutes = step.TimerMinutes,
                    IngredientReferences = step.IngredientReferences
                        .OrderBy(reference => reference.Order)
                        .Select(reference => new RecipeCardStepIngredient
                        {
                            IngredientKey = reference.RecipeIngredientId is int ingredientId && ingredientKeys.TryGetValue(ingredientId, out var key)
                                ? key
                                : string.Empty,
                            IngredientName = reference.Ingredient?.Name ?? reference.IngredientName,
                            QuantityText = reference.QuantityText,
                            Unit = reference.Unit
                        })
                        .ToList()
                })
                .ToList(),
            Notes = recipe.Notes
                .OrderBy(note => note.Section)
                .ThenBy(note => note.Order)
                .Select(note => new RecipeCardNote
                {
                    Section = note.Section,
                    Content = note.Content,
                    CreatedAt = note.CreatedAt
                })
                .ToList(),
            PlanningMetadata = recipe.PlanningMetadata
                .Select(metadata => new RecipeCardPlanningMetadata
                {
                    Kind = metadata.Kind,
                    Value = metadata.Value,
                    Notes = metadata.Notes,
                    CreatedAt = metadata.CreatedAt
                })
                .ToList(),
            CookingHistory = recipe.CookingHistory
                .Select(session => new RecipeCardCookingSession
                {
                    CookedAt = session.CookedAt,
                    PlannedServings = session.PlannedServings,
                    ActualServings = session.ActualServings,
                    Rating = session.Rating,
                    Notes = session.Notes
                })
                .ToList()
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string IngredientKey(string name, int order)
    {
        var normalized = FoodText.Normalize(name)
            .Replace(" ", "-", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(normalized)
            ? $"ingredient-{order}"
            : $"ingredient-{order}-{normalized}";
    }

    private static string UniqueName(string name, HashSet<string> existingNames)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Recepta importada" : name.Trim();
        if (existingNames.Add(baseName))
        {
            return baseName;
        }

        var importedName = $"{baseName} (importada)";
        if (existingNames.Add(importedName))
        {
            return importedName;
        }

        var index = 2;
        while (true)
        {
            var candidate = $"{baseName} (importada {index})";
            if (existingNames.Add(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static bool IsBlank(RecipeCardIngredient ingredient) =>
        string.IsNullOrWhiteSpace(ingredient.Name)
        && string.IsNullOrWhiteSpace(ingredient.Quantity)
        && string.IsNullOrWhiteSpace(ingredient.Unit);

    private static bool IsBlank(RecipeCardStep step) =>
        string.IsNullOrWhiteSpace(step.Title)
        && string.IsNullOrWhiteSpace(step.Instruction)
        && step.TimerMinutes is null
        && step.IngredientReferences.Count == 0;

    private static bool IsBlank(RecipeCardStepIngredient reference) =>
        string.IsNullOrWhiteSpace(reference.IngredientKey)
        && string.IsNullOrWhiteSpace(reference.IngredientName)
        && string.IsNullOrWhiteSpace(reference.QuantityText)
        && string.IsNullOrWhiteSpace(reference.Unit);

    private static bool IsBlank(RecipeCardPlanningMetadata metadata) =>
        string.IsNullOrWhiteSpace(metadata.Kind)
        && string.IsNullOrWhiteSpace(metadata.Value)
        && string.IsNullOrWhiteSpace(metadata.Notes);

    private static bool IsIncomplete(Recipe recipe) =>
        string.IsNullOrWhiteSpace(recipe.Category)
        || recipe.Ingredients.Count == 0
        || recipe.Steps.Count == 0
        || recipe.PreparationTimeMinutes <= 0
        || recipe.CookingTimeMinutes <= 0
        || recipe.Servings <= 0;

    private static bool IsKnownStatus(string status) =>
        string.Equals(status, RecipeStatus.Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, RecipeStatus.Draft, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStatus(string status) =>
        string.Equals(status, RecipeStatus.Active, StringComparison.OrdinalIgnoreCase)
            ? RecipeStatus.Active
            : string.Equals(status, RecipeStatus.Draft, StringComparison.OrdinalIgnoreCase)
                ? RecipeStatus.Draft
                : string.Empty;

    private static bool IsKnownScalingMode(string scalingMode) =>
        string.IsNullOrWhiteSpace(scalingMode)
        || scalingMode is IngredientScalingMode.Linear
            or IngredientScalingMode.Fixed
            or IngredientScalingMode.Approximate
            or IngredientScalingMode.ToTaste;

    private static string NormalizeScalingMode(string scalingMode) =>
        scalingMode switch
        {
            IngredientScalingMode.Fixed => IngredientScalingMode.Fixed,
            IngredientScalingMode.Approximate => IngredientScalingMode.Approximate,
            IngredientScalingMode.ToTaste => IngredientScalingMode.ToTaste,
            _ => IngredientScalingMode.Linear
        };

    private static bool IsKnownPlanningKind(string kind) =>
        kind is RecipePlanningMetadataKind.WeeklyFavourite
            or RecipePlanningMetadataKind.Fortnightly
            or RecipePlanningMetadataKind.Monthly
            or RecipePlanningMetadataKind.Seasonal
            or RecipePlanningMetadataKind.SpecialOccasion;

    private static string NormalizePlanningKind(string kind) =>
        IsKnownPlanningKind(kind) ? kind : RecipePlanningMetadataKind.Monthly;
}

public sealed class RecipeCardFile
{
    public string Application { get; set; } = "Nasdanus";
    public string Format { get; set; } = "recipe-card";
    public int FormatVersion { get; set; } = 1;
    public bool Template { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<string> RequiredFields { get; set; } = [];
    public List<string> FieldsForCompleteRecipe { get; set; } = [];
    public RecipeCardAcceptedValues AcceptedValues { get; set; } = RecipeCardAcceptedValues.Create();
    public List<RecipeCard> Recipes { get; set; } = [];
}

public sealed class RecipeCardAcceptedValues
{
    public List<string> Status { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public List<string> IngredientUnits { get; set; } = [];
    public List<string> IngredientScalingModes { get; set; } = [];
    public List<string> NoteSections { get; set; } = [];
    public List<string> PlanningKinds { get; set; } = [];
    public string Difficulty { get; set; } = "0..5";
    public string Rating { get; set; } = "null o 0..5";
    public string CategoryRule { get; set; } = "Es poden usar aquests valors o categories personalitzades.";
    public string UnitRule { get; set; } = "Es poden usar aquests valors, deixar-ho buit o posar una unitat personalitzada.";

    public static RecipeCardAcceptedValues Create() => new()
    {
        Status = [RecipeStatus.Active, RecipeStatus.Draft],
        Categories = RecipeCategory.All.ToList(),
        IngredientUnits = Nasdanus.Domain.IngredientUnits.All.Select(unit => unit.Value).ToList(),
        IngredientScalingModes =
        [
            IngredientScalingMode.Linear,
            IngredientScalingMode.Fixed,
            IngredientScalingMode.Approximate,
            IngredientScalingMode.ToTaste
        ],
        NoteSections = RecipeNoteSection.DisplayOrder.ToList(),
        PlanningKinds =
        [
            RecipePlanningMetadataKind.WeeklyFavourite,
            RecipePlanningMetadataKind.Fortnightly,
            RecipePlanningMetadataKind.Monthly,
            RecipePlanningMetadataKind.Seasonal,
            RecipePlanningMetadataKind.SpecialOccasion
        ]
    };
}

public sealed class RecipeCard
{
    public int? SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public int Difficulty { get; set; }
    public int Servings { get; set; }
    public bool IsFavourite { get; set; }
    public int? Rating { get; set; }
    public string SeasonalRecommendation { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<RecipeCardIngredient> Ingredients { get; set; } = [];
    public List<RecipeCardStep> Steps { get; set; } = [];
    public List<RecipeCardNote> Notes { get; set; } = [];
    public List<RecipeCardPlanningMetadata> PlanningMetadata { get; set; } = [];
    public List<RecipeCardCookingSession> CookingHistory { get; set; } = [];
}

public sealed class RecipeCardIngredient
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ScalingMode { get; set; } = IngredientScalingMode.Linear;
    public string? KnownIngredientName { get; set; }
}

public sealed class RecipeCardStep
{
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public List<RecipeCardStepIngredient> IngredientReferences { get; set; } = [];
}

public sealed class RecipeCardStepIngredient
{
    public string IngredientKey { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string QuantityText { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public sealed class RecipeCardNote
{
    public string Section { get; set; } = RecipeNoteSection.General;
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public sealed class RecipeCardPlanningMetadata
{
    public string Kind { get; set; } = RecipePlanningMetadataKind.Monthly;
    public string Value { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public sealed class RecipeCardCookingSession
{
    public DateTime CookedAt { get; set; }
    public int? PlannedServings { get; set; }
    public int? ActualServings { get; set; }
    public int? Rating { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class RecipeExchangeSummary
{
    public int Recipes { get; set; }
    public int DraftRecipes { get; set; }
    public int Ingredients { get; set; }
    public int Steps { get; set; }
    public int Notes { get; set; }
    public int Tags { get; set; }

    public static RecipeExchangeSummary FromCards(IEnumerable<RecipeCard> cards)
    {
        var list = cards.ToList();
        return new RecipeExchangeSummary
        {
            Recipes = list.Count,
            DraftRecipes = list.Count(card => string.Equals(card.Status, RecipeStatus.Draft, StringComparison.OrdinalIgnoreCase)),
            Ingredients = list.Sum(card => card.Ingredients.Count(ingredient => !string.IsNullOrWhiteSpace(ingredient.Name))),
            Steps = list.Sum(card => card.Steps.Count(step => !string.IsNullOrWhiteSpace(step.Title) || !string.IsNullOrWhiteSpace(step.Instruction))),
            Notes = list.Sum(card => card.Notes.Count(note => !string.IsNullOrWhiteSpace(note.Content))),
            Tags = list.Sum(card => card.Tags.Count(tag => !string.IsNullOrWhiteSpace(tag)))
        };
    }

    public static RecipeExchangeSummary FromRecipes(IEnumerable<Recipe> recipes)
    {
        var list = recipes.ToList();
        return new RecipeExchangeSummary
        {
            Recipes = list.Count,
            DraftRecipes = list.Count(recipe => recipe.IsDraft),
            Ingredients = list.Sum(recipe => recipe.Ingredients.Count),
            Steps = list.Sum(recipe => recipe.Steps.Count),
            Notes = list.Sum(recipe => recipe.Notes.Count),
            Tags = list.Sum(recipe => recipe.Tags.Count)
        };
    }
}

public sealed class RecipeImportValidationResult
{
    public bool IsValid { get; init; }
    public RecipeExchangeSummary? Summary { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<RecipeCard> Cards { get; init; } = [];

    public static RecipeImportValidationResult Valid(List<RecipeCard> cards, RecipeExchangeSummary summary) => new()
    {
        IsValid = true,
        Summary = summary,
        Cards = cards
    };

    public static RecipeImportValidationResult Invalid(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}

public sealed class RecipeImportResult
{
    public bool IsValid { get; init; }
    public RecipeExchangeSummary? Summary { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<int> ImportedRecipeIds { get; init; } = [];
    public List<string> ImportedRecipeNames { get; init; } = [];

    public static RecipeImportResult Valid(RecipeExchangeSummary summary, List<int> recipeIds, List<string> recipeNames) => new()
    {
        IsValid = true,
        Summary = summary,
        ImportedRecipeIds = recipeIds,
        ImportedRecipeNames = recipeNames
    };

    public static RecipeImportResult Invalid(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
