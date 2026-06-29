using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class RecipeService(BrowserAppStore store)
{
    public async Task<List<Recipe>> GetAllAsync()
    {
        var state = await store.GetStateAsync();
        return state.Recipes
            .OrderBy(recipe => recipe.Name)
            .Select(store.CloneRecipe)
            .ToList();
    }

    public async Task<Recipe?> GetByIdAsync(int id)
    {
        var state = await store.GetStateAsync();
        var recipe = store.FindRecipe(state, id);
        return recipe is null ? null : store.CloneRecipe(recipe);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        var state = await store.GetStateAsync();
        return state.Recipes
            .Select(recipe => recipe.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();
    }

    public async Task<Recipe> CreateQuickDraftAsync(QuickAddRecipeRequest request)
    {
        var state = await store.GetStateAsync();
        var recipe = new Recipe
        {
            Id = store.NextId(state),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            PreparationTimeMinutes = request.PreparationTimeMinutes ?? 0,
            CookingTimeMinutes = request.CookingTimeMinutes ?? 0,
            Difficulty = 1,
            Servings = request.Servings ?? 0
        };

        recipe.Ingredients = ParseLines(request.IngredientsText)
            .Select((line, index) => new RecipeIngredient
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Name = line,
                Order = index + 1
            })
            .ToList();

        recipe.Steps = ParseLines(request.StepsText)
            .Select((line, index) => new RecipeStep
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Title = $"Pas {index + 1}",
                Instruction = line,
                Order = index + 1
            })
            .ToList();

        recipe.Status = IsIncomplete(request, recipe.Ingredients.Count, recipe.Steps.Count)
            ? RecipeStatus.Draft
            : RecipeStatus.Active;

        state.Recipes.Add(recipe);
        await store.SaveAsync();

        return store.CloneRecipe(recipe);
    }

    public async Task ConsolidateAsync(int id)
    {
        var state = await store.GetStateAsync();
        var recipe = store.FindRecipe(state, id);
        if (recipe is null)
        {
            return;
        }

        recipe.Status = RecipeStatus.Active;
        await store.SaveAsync();
    }

    public async Task<Recipe?> CreateVariationAsync(int id)
    {
        var state = await store.GetStateAsync();
        var source = store.FindRecipe(state, id);
        if (source is null)
        {
            return null;
        }

        var variation = new Recipe
        {
            Id = store.NextId(state),
            Name = $"{source.Name} (variacio)",
            Description = source.Description,
            Category = source.Category,
            Status = RecipeStatus.Draft,
            PreparationTimeMinutes = source.PreparationTimeMinutes,
            CookingTimeMinutes = source.CookingTimeMinutes,
            Difficulty = source.Difficulty,
            Servings = source.Servings,
            VariationOfRecipeId = source.Id
        };

        var ingredientMap = new Dictionary<int, RecipeIngredient>();
        foreach (var sourceIngredient in source.Ingredients.OrderBy(ingredient => ingredient.Order))
        {
            var ingredient = new RecipeIngredient
            {
                Id = store.NextId(state),
                RecipeId = variation.Id,
                IngredientId = sourceIngredient.IngredientId,
                Ingredient = sourceIngredient.Ingredient,
                Order = sourceIngredient.Order,
                Name = sourceIngredient.Name,
                Quantity = sourceIngredient.Quantity,
                Unit = sourceIngredient.Unit,
                ScalingMode = sourceIngredient.ScalingMode
            };

            variation.Ingredients.Add(ingredient);
            ingredientMap[sourceIngredient.Id] = ingredient;
        }

        foreach (var sourceStep in source.Steps.OrderBy(step => step.Order))
        {
            var step = new RecipeStep
            {
                Id = store.NextId(state),
                RecipeId = variation.Id,
                Order = sourceStep.Order,
                Title = sourceStep.Title,
                Instruction = sourceStep.Instruction,
                TimerMinutes = sourceStep.TimerMinutes
            };

            foreach (var sourceReference in sourceStep.IngredientReferences.OrderBy(reference => reference.Order))
            {
                var sourceIngredientId = sourceReference.RecipeIngredientId ?? sourceReference.Ingredient?.Id;
                var ingredient = sourceIngredientId is int ingredientId && ingredientMap.TryGetValue(ingredientId, out var mapped)
                    ? mapped
                    : null;

                step.IngredientReferences.Add(new RecipeStepIngredientReference
                {
                    Id = store.NextId(state),
                    RecipeStepId = step.Id,
                    RecipeIngredientId = ingredient?.Id,
                    Ingredient = ingredient,
                    IngredientName = ingredient?.DisplayName ?? sourceReference.IngredientName,
                    Quantity = sourceReference.Quantity,
                    QuantityText = sourceReference.QuantityText,
                    Unit = sourceReference.Unit,
                    Order = sourceReference.Order
                });
            }

            variation.Steps.Add(step);
        }

        variation.Notes = source.Notes
            .OrderBy(note => note.Section)
            .ThenBy(note => note.Order)
            .Select(note => new RecipeNote
            {
                Id = store.NextId(state),
                RecipeId = variation.Id,
                Section = note.Section,
                Content = note.Content,
                Order = note.Order
            })
            .ToList();

        variation.Tags = source.Tags
            .Select(tag => new RecipeTag
            {
                Id = store.NextId(state),
                RecipeId = variation.Id,
                Name = tag.Name
            })
            .ToList();

        state.Recipes.Add(variation);
        await store.SaveAsync();

        return store.CloneRecipe(variation);
    }

    public async Task UpdateRecipeAsync(int id, RecipeEditRequest request)
    {
        var state = await store.GetStateAsync();
        var recipe = store.FindRecipe(state, id);
        if (recipe is null)
        {
            return;
        }

        recipe.Name = request.Name.Trim();
        recipe.Description = request.Description.Trim();
        recipe.Category = request.Category.Trim();
        recipe.Servings = Math.Max(0, request.Servings);
        recipe.PreparationTimeMinutes = Math.Max(0, request.PreparationTimeMinutes);
        recipe.CookingTimeMinutes = Math.Max(0, request.CookingTimeMinutes);
        recipe.Difficulty = Math.Clamp(request.Difficulty, 0, 5);

        recipe.Ingredients = [];
        recipe.Steps = [];
        recipe.Notes = [];

        var ingredientMap = new Dictionary<string, RecipeIngredient>(StringComparer.OrdinalIgnoreCase);
        foreach (var ingredientRequest in request.Ingredients
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.Name))
            .Select((ingredient, index) => (Ingredient: ingredient, Order: index + 1)))
        {
            var ingredient = new RecipeIngredient
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Name = ingredientRequest.Ingredient.Name.Trim(),
                Quantity = ingredientRequest.Ingredient.Quantity.Trim(),
                Unit = ingredientRequest.Ingredient.Unit.Trim(),
                ScalingMode = NormalizeScalingMode(ingredientRequest.Ingredient.ScalingMode),
                Order = ingredientRequest.Order
            };
            recipe.Ingredients.Add(ingredient);
            ingredientMap[ingredientRequest.Ingredient.Key] = ingredient;
        }

        foreach (var stepRequest in request.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.Title) || !string.IsNullOrWhiteSpace(step.Instruction))
            .Select((step, index) => (Step: step, Order: index + 1)))
        {
            var step = new RecipeStep
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Title = stepRequest.Step.Title.Trim(),
                Instruction = stepRequest.Step.Instruction.Trim(),
                TimerMinutes = stepRequest.Step.TimerMinutes,
                Order = stepRequest.Order
            };

            foreach (var referenceRequest in stepRequest.Step.IngredientReferences
                .Where(reference => ingredientMap.ContainsKey(reference.IngredientKey))
                .Select((reference, index) => (Reference: reference, Order: index + 1)))
            {
                var ingredient = ingredientMap[referenceRequest.Reference.IngredientKey];
                step.IngredientReferences.Add(new RecipeStepIngredientReference
                {
                    Id = store.NextId(state),
                    RecipeStepId = step.Id,
                    RecipeIngredientId = ingredient.Id,
                    Ingredient = ingredient,
                    IngredientName = ingredient.Name,
                    Quantity = IngredientScaling.ParseQuantity(referenceRequest.Reference.QuantityText),
                    QuantityText = referenceRequest.Reference.QuantityText.Trim(),
                    Unit = referenceRequest.Reference.Unit.Trim(),
                    Order = referenceRequest.Order
                });
            }

            recipe.Steps.Add(step);
        }

        foreach (var noteRequest in request.Notes
            .Where(note => !string.IsNullOrWhiteSpace(note.Content))
            .Select((note, index) => (Note: note, Order: index + 1)))
        {
            recipe.Notes.Add(new RecipeNote
            {
                Id = store.NextId(state),
                RecipeId = recipe.Id,
                Section = NormalizeNoteSection(noteRequest.Note.Section),
                Content = noteRequest.Note.Content.Trim(),
                Order = noteRequest.Order
            });
        }

        if (!IsKnownStatus(recipe.Status))
        {
            recipe.Status = IsIncomplete(recipe)
                ? RecipeStatus.Draft
                : RecipeStatus.Active;
        }

        await store.SaveAsync();
    }

    private static bool IsIncomplete(QuickAddRecipeRequest request, int ingredientCount, int stepCount) =>
        string.IsNullOrWhiteSpace(request.Category)
        || ingredientCount == 0
        || stepCount == 0
        || request.PreparationTimeMinutes is null
        || request.CookingTimeMinutes is null
        || request.Servings is null;

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

    private static string NormalizeScalingMode(string scalingMode) =>
        scalingMode switch
        {
            IngredientScalingMode.Fixed => IngredientScalingMode.Fixed,
            IngredientScalingMode.Approximate => IngredientScalingMode.Approximate,
            IngredientScalingMode.ToTaste => IngredientScalingMode.ToTaste,
            _ => IngredientScalingMode.Linear
        };

    private static string NormalizeNoteSection(string section) =>
        RecipeNoteSection.DisplayOrder.Contains(section)
            ? section
            : RecipeNoteSection.General;

    private static List<string> ParseLines(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(CleanLinePrefix)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToList();

    private static string CleanLinePrefix(string line)
    {
        var cleaned = line.Trim().TrimStart('-', '*', '•').Trim();
        var dotIndex = cleaned.IndexOf('.');
        if (dotIndex > 0 && dotIndex <= 2 && cleaned[..dotIndex].All(char.IsDigit))
        {
            cleaned = cleaned[(dotIndex + 1)..].Trim();
        }

        return cleaned;
    }
}
