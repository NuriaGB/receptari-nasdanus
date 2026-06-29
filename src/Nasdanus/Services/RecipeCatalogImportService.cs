using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nasdanus.Data;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class RecipeCatalogImportService(
    IDbContextFactory<NasdanusDbContext> dbContextFactory,
    IWebHostEnvironment environment,
    ILogger<RecipeCatalogImportService> logger)
{
    private const int DefaultImportedServings = 3;

    public async Task ImportPreparedCatalogsAsync()
    {
        var outputPath = ResolveOutputPath();
        var airunPath = Path.Combine(outputPath, "extracted_recipes.json");
        var xupxupPath = Path.Combine(outputPath, "xupxup_saved_recipes.json");

        if (!File.Exists(airunPath) && !File.Exists(xupxupPath))
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existingRecipeKeys = (await db.Recipes
                .Select(recipe => recipe.Name)
                .AsNoTracking()
                .ToListAsync())
            .Select(NormalizeRecipeName)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importedCount = 0;
        if (File.Exists(airunPath))
        {
            importedCount += await ImportAirunAsync(db, airunPath, existingRecipeKeys);
        }

        if (File.Exists(xupxupPath))
        {
            importedCount += await ImportXupxupAsync(db, xupxupPath, existingRecipeKeys);
        }

        if (importedCount == 0)
        {
            return;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Imported {RecipeCount} prepared recipes into Nasdanus.", importedCount);
    }

    private async Task<int> ImportAirunAsync(NasdanusDbContext db, string filePath, HashSet<string> existingRecipeKeys)
    {
        using var document = await ReadJsonDocumentAsync(filePath);
        if (!TryGetArray(document.RootElement, "recipes", out var recipes))
        {
            return 0;
        }

        var importedCount = 0;
        foreach (var recipeElement in recipes.EnumerateArray())
        {
            var name = TrimTo(Text(recipeElement, "name"), 160);
            if (!TryReserveRecipeName(name, existingRecipeKeys))
            {
                continue;
            }

            var ingredients = IngredientsFromAirun(recipeElement);
            var steps = StepsFromTextArray(recipeElement, "preparation_steps");
            var notes = NotesFromAirun(recipeElement);
            var recipe = new Recipe
            {
                Name = name,
                Description = TrimTo(Text(recipeElement, "description"), 600),
                Category = TrimTo(Text(recipeElement, "category", "Airun"), 64),
                Servings = DefaultImportedServings,
                Difficulty = 1,
                Ingredients = ingredients,
                Steps = steps,
                Notes = notes
            };

            recipe.Status = IsIncomplete(recipe) ? RecipeStatus.Draft : RecipeStatus.Active;
            db.Recipes.Add(recipe);
            importedCount++;
        }

        return importedCount;
    }

    private async Task<int> ImportXupxupAsync(NasdanusDbContext db, string filePath, HashSet<string> existingRecipeKeys)
    {
        using var document = await ReadJsonDocumentAsync(filePath);
        if (!TryGetArray(document.RootElement, "recipes", out var recipes))
        {
            return 0;
        }

        var importedCount = 0;
        foreach (var recipeElement in recipes.EnumerateArray())
        {
            var name = TrimTo(Text(recipeElement, "name"), 160);
            if (!TryReserveRecipeName(name, existingRecipeKeys))
            {
                continue;
            }

            var ingredientBySourceId = new Dictionary<string, RecipeIngredient>(StringComparer.OrdinalIgnoreCase);
            var ingredients = IngredientsFromXupxup(recipeElement, ingredientBySourceId);
            var steps = StepsFromXupxup(recipeElement, ingredientBySourceId);
            var notes = NotesFromXupxup(recipeElement);
            var recipe = new Recipe
            {
                Name = name,
                Description = TrimTo(Text(recipeElement, "description"), 600),
                Category = TrimTo(FirstArrayText(recipeElement, "categories", "Xupxup"), 64),
                Servings = Math.Max(0, IntValue(recipeElement, "servings") ?? DefaultImportedServings),
                PreparationTimeMinutes = Math.Max(0, IntValue(recipeElement, "prep_time_minutes") ?? 0),
                CookingTimeMinutes = Math.Max(0, IntValue(recipeElement, "cook_time_minutes") ?? 0),
                Difficulty = Math.Clamp(IntValue(recipeElement, "difficulty") ?? 1, 0, 5),
                Ingredients = ingredients,
                Steps = steps,
                Notes = notes
            };

            recipe.Status = IsIncomplete(recipe) ? RecipeStatus.Draft : RecipeStatus.Active;
            db.Recipes.Add(recipe);
            importedCount++;
        }

        return importedCount;
    }

    private string ResolveOutputPath()
    {
        var appRoot = environment.ContentRootPath;
        var repositoryRoot = Directory.GetParent(appRoot)?.Parent?.FullName ?? appRoot;
        return Path.Combine(repositoryRoot, "output");
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonDocument.ParseAsync(stream);
    }

    private static List<RecipeIngredient> IngredientsFromAirun(JsonElement recipeElement)
    {
        if (!TryGetArray(recipeElement, "ingredient_candidates", out var candidates))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ingredients = new List<RecipeIngredient>();
        foreach (var candidate in candidates.EnumerateArray())
        {
            var name = TrimTo(Text(candidate, "canonical_name"), 120);
            var key = IngredientNameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(key))
            {
                continue;
            }

            ingredients.Add(new RecipeIngredient
            {
                Name = name,
                ScalingMode = InferScalingMode(name),
                Order = ingredients.Count + 1
            });
        }

        return ingredients;
    }

    private static List<RecipeIngredient> IngredientsFromXupxup(
        JsonElement recipeElement,
        Dictionary<string, RecipeIngredient> ingredientBySourceId)
    {
        if (!TryGetArray(recipeElement, "ingredients", out var ingredientsElement))
        {
            return [];
        }

        var ingredients = ingredientsElement.EnumerateArray()
            .Select((ingredientElement, index) => new
            {
                Element = ingredientElement,
                Order = IntValue(ingredientElement, "order") ?? index + 1
            })
            .OrderBy(item => item.Order)
            .ToList();

        var importedIngredients = new List<RecipeIngredient>();
        foreach (var ingredientItem in ingredients)
        {
            var name = TrimTo(Text(ingredientItem.Element, "name"), 120);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var ingredient = new RecipeIngredient
            {
                Name = name,
                Quantity = TrimTo(Text(ingredientItem.Element, "quantity"), 32),
                Unit = TrimTo(Text(ingredientItem.Element, "unit"), 48),
                ScalingMode = InferScalingMode(name),
                Order = importedIngredients.Count + 1
            };
            importedIngredients.Add(ingredient);

            var sourceId = Text(ingredientItem.Element, "id");
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                ingredientBySourceId[sourceId] = ingredient;
            }
        }

        return importedIngredients;
    }

    private static List<RecipeStep> StepsFromTextArray(JsonElement recipeElement, string propertyName)
    {
        if (!TryGetArray(recipeElement, propertyName, out var stepsElement))
        {
            return [];
        }

        return stepsElement.EnumerateArray()
            .Select(stepElement => stepElement.ValueKind == JsonValueKind.String ? stepElement.GetString() ?? string.Empty : string.Empty)
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select((step, index) => new RecipeStep
            {
                Title = $"Pas {index + 1}",
                Instruction = TrimTo(step.Trim(), 1200),
                Order = index + 1
            })
            .ToList();
    }

    private static List<RecipeStep> StepsFromXupxup(
        JsonElement recipeElement,
        IReadOnlyDictionary<string, RecipeIngredient> ingredientBySourceId)
    {
        if (!TryGetArray(recipeElement, "steps", out var stepsElement))
        {
            return [];
        }

        var steps = stepsElement.EnumerateArray()
            .Select((stepElement, index) => new
            {
                Element = stepElement,
                Order = IntValue(stepElement, "step_number") ?? index + 1
            })
            .OrderBy(item => item.Order)
            .ToList();

        var importedSteps = new List<RecipeStep>();
        foreach (var stepItem in steps)
        {
            var instruction = TrimTo(Text(stepItem.Element, "description"), 1200);
            if (string.IsNullOrWhiteSpace(instruction))
            {
                continue;
            }

            var step = new RecipeStep
            {
                Title = $"Pas {importedSteps.Count + 1}",
                Instruction = instruction,
                TimerMinutes = IntValue(stepItem.Element, "timer_minutes"),
                Order = importedSteps.Count + 1
            };

            AddXupxupIngredientReferences(stepItem.Element, ingredientBySourceId, step);
            importedSteps.Add(step);
        }

        return importedSteps;
    }

    private static void AddXupxupIngredientReferences(
        JsonElement stepElement,
        IReadOnlyDictionary<string, RecipeIngredient> ingredientBySourceId,
        RecipeStep step)
    {
        if (!TryGetArray(stepElement, "linked_ingredients", out var referencesElement))
        {
            return;
        }

        foreach (var referenceElement in referencesElement.EnumerateArray())
        {
            var ingredientId = Text(referenceElement, "ingredientId");
            if (!string.IsNullOrWhiteSpace(ingredientId)
                && ingredientBySourceId.TryGetValue(ingredientId, out var ingredient))
            {
                step.IngredientReferences.Add(new RecipeStepIngredientReference
                {
                    Ingredient = ingredient,
                    IngredientName = ingredient.Name,
                    Quantity = IngredientScaling.ParseQuantity(ingredient.Quantity),
                    QuantityText = ingredient.Quantity,
                    Unit = ingredient.Unit,
                    Order = step.IngredientReferences.Count + 1
                });
                continue;
            }

            var displayText = TrimTo(Text(referenceElement, "displayText"), 120);
            if (!string.IsNullOrWhiteSpace(displayText))
            {
                step.IngredientReferences.Add(new RecipeStepIngredientReference
                {
                    IngredientName = displayText,
                    Order = step.IngredientReferences.Count + 1
                });
            }
        }
    }

    private static List<RecipeNote> NotesFromAirun(JsonElement recipeElement)
    {
        if (!TryGetArray(recipeElement, "notes", out var notesElement))
        {
            return [];
        }

        var notes = new List<RecipeNote>();
        foreach (var noteElement in notesElement.EnumerateArray())
        {
            var text = Text(noteElement, "text");
            var label = Text(noteElement, "label");
            var content = string.IsNullOrWhiteSpace(label) ? text : $"{label}: {text}";
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            notes.Add(new RecipeNote
            {
                Section = RecipeNoteSection.General,
                Content = TrimTo(content, 1200),
                Order = notes.Count + 1
            });
        }

        return notes;
    }

    private static List<RecipeNote> NotesFromXupxup(JsonElement recipeElement)
    {
        var tips = Text(recipeElement, "tips");
        if (string.IsNullOrWhiteSpace(tips))
        {
            return [];
        }

        return
        [
            new RecipeNote
            {
                Section = RecipeNoteSection.CookingTips,
                Content = TrimTo(tips, 1200),
                Order = 1
            }
        ];
    }

    private static bool TryReserveRecipeName(string name, HashSet<string> existingRecipeKeys)
    {
        var key = NormalizeRecipeName(name);
        return !string.IsNullOrWhiteSpace(key) && existingRecipeKeys.Add(key);
    }

    private static string NormalizeRecipeName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)))
            {
                builder.Append(char.IsWhiteSpace(character) ? ' ' : character);
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsIncomplete(Recipe recipe) =>
        string.IsNullOrWhiteSpace(recipe.Category)
        || recipe.Ingredients.Count == 0
        || recipe.Steps.Count == 0
        || recipe.PreparationTimeMinutes <= 0
        || recipe.CookingTimeMinutes <= 0
        || recipe.Servings <= 0;

    private static string InferScalingMode(string ingredientName)
    {
        var normalized = ingredientName.ToLowerInvariant();
        if (normalized == "sal"
            || normalized.Contains("pebre", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("jalape", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("bitxo", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("xili", StringComparison.OrdinalIgnoreCase))
        {
            return IngredientScalingMode.ToTaste;
        }

        if (normalized.Contains("oli", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("all", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("gingebre", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("herba", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("julivert", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("llimona", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("curcuma", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("cúrcuma", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("garam masala", StringComparison.OrdinalIgnoreCase))
        {
            return IngredientScalingMode.Approximate;
        }

        return IngredientScalingMode.Linear;
    }

    private static string FirstArrayText(JsonElement element, string propertyName, string fallback = "")
    {
        if (!TryGetArray(element, propertyName, out var array))
        {
            return fallback;
        }

        foreach (var item in array.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return fallback;
    }

    private static string Text(JsonElement element, string propertyName, string fallback = "")
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim() ?? fallback,
            JsonValueKind.Number => property.ToString(),
            _ => fallback
        };
    }

    private static int? IntValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => null
        };
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static string TrimTo(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].Trim();
    }
}
