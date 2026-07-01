using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class BrowserAppStore(HttpClient httpClient, IJSRuntime jsRuntime)
{
    private const string StorageKey = "nasdanus.static.state.v1";
    private const string BackupStorageKey = "nasdanus.static.state.backup.v1";
    private const string LastSavedAtStorageKey = "nasdanus.static.lastSavedAt.v1";
    private const string BackupApplicationName = "Nasdanus";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonOptions)
    {
        WriteIndented = true
    };

    private LocalAppState? state;
    private IngredientKnowledgeFile? ingredientKnowledge;

    public async Task<LocalAppState> GetStateAsync()
    {
        if (state is not null)
        {
            return state;
        }

        state = await LoadStoredStateAsync();

        Normalize(state);
        await MergeIngredientKnowledgeAsync(state);
        Normalize(state);
        return state;
    }

    public async Task SaveAsync()
    {
        if (state is null)
        {
            return;
        }

        Normalize(state);
        var snapshot = CreateSnapshot(state);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var currentJson = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", BackupStorageKey, currentJson);
        }

        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", LastSavedAtStorageKey, DateTime.UtcNow.ToString("O"));
    }

    public async Task<string> ExportJsonAsync()
    {
        var appState = await GetStateAsync();
        var snapshot = CreateSnapshot(appState);
        var backup = new NasdanusBackupFile
        {
            ExportedAt = DateTime.UtcNow,
            SchemaVersion = snapshot.SchemaVersion,
            Data = snapshot,
            Summary = DataBackupSummary.From(snapshot)
        };

        return JsonSerializer.Serialize(backup, ExportJsonOptions);
    }

    public async Task<DataBackupSummary> GetSummaryAsync()
    {
        var appState = await GetStateAsync();
        return DataBackupSummary.From(appState);
    }

    public async Task<DateTime?> GetLastSavedAtAsync()
    {
        var stored = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", LastSavedAtStorageKey);
        return DateTime.TryParse(stored, out var savedAt)
            ? savedAt
            : null;
    }

    public Task<DataImportValidationResult> ValidateImportJsonAsync(string json)
    {
        var errors = new List<string>();
        var importedState = DeserializeImportState(json, errors);
        if (importedState is null)
        {
            errors.Add("El fitxer no sembla una copia valida de Nasdanus.");
            return Task.FromResult(DataImportValidationResult.Invalid(errors));
        }

        Normalize(importedState);
        errors.AddRange(ValidateState(importedState));
        return Task.FromResult(errors.Count == 0
            ? DataImportValidationResult.Valid(importedState, DataBackupSummary.From(importedState))
            : DataImportValidationResult.Invalid(errors));
    }

    public async Task<DataImportValidationResult> ReplaceStateFromJsonAsync(string json)
    {
        var validation = await ValidateImportJsonAsync(json);
        if (!validation.IsValid || validation.State is null)
        {
            return validation;
        }

        state = validation.State;
        await MergeIngredientKnowledgeAsync(state);
        await SaveAsync();
        return validation;
    }

    public int NextId(LocalAppState appState) => appState.NextId++;

    public Recipe? FindRecipe(LocalAppState appState, int recipeId) =>
        appState.Recipes.FirstOrDefault(recipe => recipe.Id == recipeId);

    public Ingredient? FindIngredient(LocalAppState appState, int ingredientId) =>
        appState.Ingredients.FirstOrDefault(ingredient => ingredient.Id == ingredientId);

    public Recipe CloneRecipe(Recipe recipe)
    {
        var ingredients = recipe.Ingredients
            .OrderBy(ingredient => ingredient.Order)
            .Select(ingredient => new RecipeIngredient
            {
                Id = ingredient.Id,
                RecipeId = recipe.Id,
                IngredientId = ingredient.IngredientId,
                Ingredient = ingredient.Ingredient is null ? null : CloneIngredient(ingredient.Ingredient),
                Order = ingredient.Order,
                Name = ingredient.Name,
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
                ScalingMode = ingredient.ScalingMode
            })
            .ToList();
        var ingredientById = ingredients.ToDictionary(ingredient => ingredient.Id);

        var steps = recipe.Steps
            .OrderBy(step => step.Order)
            .Select(step =>
            {
                var clone = new RecipeStep
                {
                    Id = step.Id,
                    RecipeId = recipe.Id,
                    Order = step.Order,
                    Title = step.Title,
                    Instruction = step.Instruction,
                    TimerMinutes = step.TimerMinutes
                };

                clone.IngredientReferences = step.IngredientReferences
                    .OrderBy(reference => reference.Order)
                    .Select(reference =>
                    {
                        var ingredientId = reference.RecipeIngredientId ?? reference.Ingredient?.Id;
                        var ingredient = ingredientId is int id && ingredientById.TryGetValue(id, out var mapped)
                            ? mapped
                            : null;
                        return new RecipeStepIngredientReference
                        {
                            Id = reference.Id,
                            RecipeStepId = step.Id,
                            RecipeIngredientId = ingredient?.Id ?? ingredientId,
                            Ingredient = ingredient,
                            IngredientName = string.IsNullOrWhiteSpace(reference.IngredientName)
                                ? ingredient?.Name ?? string.Empty
                                : reference.IngredientName,
                            Quantity = reference.Quantity,
                            QuantityText = reference.QuantityText,
                            Unit = reference.Unit,
                            Order = reference.Order
                        };
                    })
                    .ToList();

                return clone;
            })
            .ToList();

        return new Recipe
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Description = recipe.Description,
            Category = recipe.Category,
            Status = recipe.Status,
            PreparationTimeMinutes = recipe.PreparationTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Difficulty = recipe.Difficulty,
            Servings = recipe.Servings,
            IsFavourite = recipe.IsFavourite,
            Rating = recipe.Rating,
            SeasonalRecommendation = recipe.SeasonalRecommendation,
            VariationOfRecipeId = recipe.VariationOfRecipeId,
            Ingredients = ingredients,
            Steps = steps,
            Notes = recipe.Notes
                .OrderBy(note => note.Section)
                .ThenBy(note => note.Order)
                .Select(note => new RecipeNote
                {
                    Id = note.Id,
                    RecipeId = recipe.Id,
                    Section = note.Section,
                    Content = note.Content,
                    Order = note.Order,
                    CreatedAt = note.CreatedAt
                })
                .ToList(),
            PlanningMetadata = recipe.PlanningMetadata
                .Select(metadata => new RecipePlanningMetadata
                {
                    Id = metadata.Id,
                    RecipeId = recipe.Id,
                    Kind = metadata.Kind,
                    Value = metadata.Value,
                    Notes = metadata.Notes,
                    CreatedAt = metadata.CreatedAt
                })
                .ToList(),
            Tags = recipe.Tags
                .Select(tag => new RecipeTag
                {
                    Id = tag.Id,
                    RecipeId = recipe.Id,
                    Name = tag.Name
                })
                .ToList(),
            CookingHistory = recipe.CookingHistory
                .Select(session => new RecipeCookingSession
                {
                    Id = session.Id,
                    RecipeId = recipe.Id,
                    CookedAt = session.CookedAt,
                    PlannedServings = session.PlannedServings,
                    ActualServings = session.ActualServings,
                    Rating = session.Rating,
                    Notes = session.Notes
                })
                .ToList()
        };
    }

    public MealPlanSlot CloneSlot(LocalAppState appState, MealPlanSlot slot) => new()
    {
        Id = slot.Id,
        Date = slot.Date,
        MealKind = slot.MealKind,
        PlannedRecipes = slot.PlannedRecipes
            .OrderBy(plannedRecipe => plannedRecipe.Order)
            .Select(plannedRecipe =>
            {
                var recipe = FindRecipe(appState, plannedRecipe.RecipeId);
                return new MealPlanRecipe
                {
                    Id = plannedRecipe.Id,
                    MealPlanSlotId = slot.Id,
                    RecipeId = plannedRecipe.RecipeId,
                    PlannedServings = plannedRecipe.PlannedServings,
                    Order = plannedRecipe.Order,
                    Recipe = recipe is null ? null : CloneRecipe(recipe)
                };
            })
            .ToList()
    };

    public ShoppingList CloneShoppingList(LocalAppState appState, ShoppingList list) => new()
    {
        Id = list.Id,
        WeekStart = list.WeekStart,
        CreatedAt = list.CreatedAt,
        UpdatedAt = list.UpdatedAt,
        Items = list.Items
            .OrderBy(item => item.Order)
            .Select(item =>
            {
                var recipe = item.RecipeId is int recipeId ? FindRecipe(appState, recipeId) : null;
                return new ShoppingListItem
                {
                    Id = item.Id,
                    ShoppingListId = list.Id,
                    Name = item.Name,
                    Category = item.Category,
                    QuantityText = item.QuantityText,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    SourceRecipeCount = item.SourceRecipeCount,
                    SourceRecipeNames = item.SourceRecipeNames,
                    IsChecked = item.IsChecked,
                    IsManual = item.IsManual,
                    IsHouseholdItem = item.IsHouseholdItem,
                    RecipeId = item.RecipeId,
                    Recipe = recipe is null ? null : CloneRecipe(recipe),
                    Order = item.Order
                };
            })
            .ToList()
    };

    private async Task<LocalAppState> LoadSeedAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<LocalAppState>("data/nasdanus-seed.json", JsonOptions)
                ?? CreateFallbackState();
        }
        catch
        {
            return CreateFallbackState();
        }
    }

    private async Task<LocalAppState> LoadStoredStateAsync()
    {
        var storedState = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        var parsedState = DeserializeStoredState(storedState);
        if (parsedState is not null)
        {
            return parsedState;
        }

        var backupState = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", BackupStorageKey);
        parsedState = DeserializeStoredState(backupState);
        return parsedState ?? await LoadSeedAsync();
    }

    private static LocalAppState CreateSnapshot(LocalAppState source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        NextId = source.NextId,
        Ingredients = source.Ingredients.Select(CreateIngredientSnapshot).ToList(),
        Products = source.Products.Select(CreateProductSnapshot).ToList(),
        Recipes = source.Recipes.Select(CreateRecipeSnapshot).ToList(),
        MealPlanSlots = source.MealPlanSlots
            .Select(slot => new MealPlanSlot
            {
                Id = slot.Id,
                Date = slot.Date,
                MealKind = slot.MealKind,
                PlannedRecipes = slot.PlannedRecipes
                    .OrderBy(plannedRecipe => plannedRecipe.Order)
                    .Select(plannedRecipe => new MealPlanRecipe
                    {
                        Id = plannedRecipe.Id,
                        MealPlanSlotId = slot.Id,
                        RecipeId = plannedRecipe.RecipeId,
                        PlannedServings = plannedRecipe.PlannedServings,
                        Order = plannedRecipe.Order
                    })
                    .ToList()
            })
            .ToList(),
        PantryItems = source.PantryItems
            .Select(item => new PantryItem
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToList(),
        RecipeIdeas = source.RecipeIdeas
            .Select(idea => new RecipeIdea
            {
                Id = idea.Id,
                WeekStart = idea.WeekStart,
                RecipeId = idea.RecipeId,
                IsDismissed = idea.IsDismissed,
                CreatedAt = idea.CreatedAt,
                UpdatedAt = idea.UpdatedAt
            })
            .ToList(),
        ProductBacklogItems = source.ProductBacklogItems
            .OrderBy(item => item.CreatedAt)
            .Select(CreateProductBacklogSnapshot)
            .ToList(),
        ShoppingLists = source.ShoppingLists
            .Select(list => new ShoppingList
            {
                Id = list.Id,
                WeekStart = list.WeekStart,
                CreatedAt = list.CreatedAt,
                UpdatedAt = list.UpdatedAt,
                Items = list.Items
                    .OrderBy(item => item.Order)
                    .Select(item => new ShoppingListItem
                    {
                        Id = item.Id,
                        ShoppingListId = list.Id,
                        Name = item.Name,
                        Category = item.Category,
                        QuantityText = item.QuantityText,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        SourceRecipeCount = item.SourceRecipeCount,
                        SourceRecipeNames = item.SourceRecipeNames,
                        IsChecked = item.IsChecked,
                        IsManual = item.IsManual,
                        IsHouseholdItem = item.IsHouseholdItem,
                        RecipeId = item.RecipeId,
                        Order = item.Order
                    })
                    .ToList()
            })
            .ToList()
    };

    private static Recipe CreateRecipeSnapshot(Recipe recipe)
    {
        var ingredientSnapshots = recipe.Ingredients
            .OrderBy(ingredient => ingredient.Order)
            .Select(ingredient => new RecipeIngredient
            {
                Id = ingredient.Id,
                RecipeId = recipe.Id,
                IngredientId = ingredient.IngredientId,
                Order = ingredient.Order,
                Name = ingredient.Name,
                Quantity = ingredient.Quantity,
                Unit = ingredient.Unit,
                ScalingMode = ingredient.ScalingMode
            })
            .ToList();

        return new Recipe
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Description = recipe.Description,
            Category = recipe.Category,
            Status = recipe.Status,
            PreparationTimeMinutes = recipe.PreparationTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Difficulty = recipe.Difficulty,
            Servings = recipe.Servings,
            IsFavourite = recipe.IsFavourite,
            Rating = recipe.Rating,
            SeasonalRecommendation = recipe.SeasonalRecommendation,
            VariationOfRecipeId = recipe.VariationOfRecipeId,
            Ingredients = ingredientSnapshots,
            Steps = recipe.Steps
                .OrderBy(step => step.Order)
                .Select(step => new RecipeStep
                {
                    Id = step.Id,
                    RecipeId = recipe.Id,
                    Order = step.Order,
                    Title = step.Title,
                    Instruction = step.Instruction,
                    TimerMinutes = step.TimerMinutes,
                    IngredientReferences = step.IngredientReferences
                        .OrderBy(reference => reference.Order)
                        .Select(reference => new RecipeStepIngredientReference
                        {
                            Id = reference.Id,
                            RecipeStepId = step.Id,
                            RecipeIngredientId = reference.RecipeIngredientId ?? reference.Ingredient?.Id,
                            IngredientName = reference.IngredientName,
                            Quantity = reference.Quantity,
                            QuantityText = reference.QuantityText,
                            Unit = reference.Unit,
                            Order = reference.Order
                        })
                        .ToList()
                })
                .ToList(),
            Notes = recipe.Notes
                .Select(note => new RecipeNote
                {
                    Id = note.Id,
                    RecipeId = recipe.Id,
                    Section = note.Section,
                    Content = note.Content,
                    Order = note.Order,
                    CreatedAt = note.CreatedAt
                })
                .ToList(),
            PlanningMetadata = recipe.PlanningMetadata
                .Select(metadata => new RecipePlanningMetadata
                {
                    Id = metadata.Id,
                    RecipeId = recipe.Id,
                    Kind = metadata.Kind,
                    Value = metadata.Value,
                    Notes = metadata.Notes,
                    CreatedAt = metadata.CreatedAt
                })
                .ToList(),
            Tags = recipe.Tags
                .Select(tag => new RecipeTag
                {
                    Id = tag.Id,
                    RecipeId = recipe.Id,
                    Name = tag.Name
                })
                .ToList(),
            CookingHistory = recipe.CookingHistory
                .Select(session => new RecipeCookingSession
                {
                    Id = session.Id,
                    RecipeId = recipe.Id,
                    CookedAt = session.CookedAt,
                    PlannedServings = session.PlannedServings,
                    ActualServings = session.ActualServings,
                    Rating = session.Rating,
                    Notes = session.Notes
                })
                .ToList()
        };
    }

    private void Normalize(LocalAppState appState)
    {
        EnsureCollections(appState);
        var maxId = 0;

        foreach (var ingredient in appState.Ingredients)
        {
            AssignId(appState, ingredient.Id, value => ingredient.Id = value, ref maxId);
            ingredient.KnowledgeId = ingredient.KnowledgeId.Trim();
            ingredient.Name = ingredient.Name.Trim();
            ingredient.Aliases ??= [];
            ingredient.Aliases = ingredient.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias)
                .ToList();
            ingredient.Category = NormalizeIngredientCategory(ingredient.Category);
            ingredient.PantryCategory = NormalizeShoppingCategory(ingredient.PantryCategory);
            EnrichIngredientNutrition(ingredient);
        }

        foreach (var product in appState.Products)
        {
            AssignId(appState, product.Id, value => product.Id = value, ref maxId);
            product.Ingredient = product.IngredientId is int ingredientId
                ? FindIngredient(appState, ingredientId)
                : null;
        }

        foreach (var recipe in appState.Recipes)
        {
            AssignId(appState, recipe.Id, value => recipe.Id = value, ref maxId);
            NormalizeRecipe(appState, recipe, ref maxId);
        }

        foreach (var slot in appState.MealPlanSlots)
        {
            AssignId(appState, slot.Id, value => slot.Id = value, ref maxId);
            foreach (var plannedRecipe in slot.PlannedRecipes)
            {
                AssignId(appState, plannedRecipe.Id, value => plannedRecipe.Id = value, ref maxId);
                plannedRecipe.MealPlanSlotId = slot.Id;
                plannedRecipe.MealPlanSlot = null;
                plannedRecipe.Recipe = FindRecipe(appState, plannedRecipe.RecipeId);
            }
        }

        foreach (var pantryItem in appState.PantryItems)
        {
            AssignId(appState, pantryItem.Id, value => pantryItem.Id = value, ref maxId);
        }

        foreach (var idea in appState.RecipeIdeas)
        {
            AssignId(appState, idea.Id, value => idea.Id = value, ref maxId);
        }

        foreach (var item in appState.ProductBacklogItems)
        {
            AssignId(appState, item.Id, value => item.Id = value, ref maxId);
            item.Labels ??= [];
            item.Context ??= new ProductBacklogContext();
            item.Context.FeedbackId ??= item.Id;
        }

        foreach (var list in appState.ShoppingLists)
        {
            AssignId(appState, list.Id, value => list.Id = value, ref maxId);
            foreach (var item in list.Items)
            {
                AssignId(appState, item.Id, value => item.Id = value, ref maxId);
                item.ShoppingListId = list.Id;
                item.ShoppingList = null;
                item.Recipe = item.RecipeId is int recipeId ? FindRecipe(appState, recipeId) : null;
            }
        }

        appState.NextId = Math.Max(appState.NextId, Math.Max(maxId + 1, 1000));
    }

    private void NormalizeRecipe(LocalAppState appState, Recipe recipe, ref int maxId)
    {
        EnsureRecipeCollections(recipe);
        foreach (var ingredient in recipe.Ingredients)
        {
            AssignId(appState, ingredient.Id, value => ingredient.Id = value, ref maxId);
            ingredient.RecipeId = recipe.Id;
            ingredient.Recipe = null;
            ingredient.Ingredient = ResolveRecipeIngredient(appState, ingredient, ref maxId);
            ingredient.IngredientId = ingredient.Ingredient?.Id;
            if (string.IsNullOrWhiteSpace(ingredient.Name))
            {
                ingredient.Name = ingredient.Ingredient?.Name ?? string.Empty;
            }
            ingredient.ScalingMode = NormalizeScalingMode(ingredient.ScalingMode);
        }

        var ingredientsById = recipe.Ingredients.ToDictionary(ingredient => ingredient.Id);
        foreach (var step in recipe.Steps)
        {
            AssignId(appState, step.Id, value => step.Id = value, ref maxId);
            step.RecipeId = recipe.Id;
            step.Recipe = null;

            foreach (var reference in step.IngredientReferences)
            {
                AssignId(appState, reference.Id, value => reference.Id = value, ref maxId);
                reference.RecipeStepId = step.Id;
                reference.Step = null;
                var ingredientId = reference.RecipeIngredientId ?? reference.Ingredient?.Id;
                reference.Ingredient = ingredientId is int id && ingredientsById.TryGetValue(id, out var ingredient)
                    ? ingredient
                    : null;
                reference.RecipeIngredientId = reference.Ingredient?.Id ?? ingredientId;
                if (string.IsNullOrWhiteSpace(reference.IngredientName))
                {
                    reference.IngredientName = reference.Ingredient?.Name ?? string.Empty;
                }
            }
        }

        foreach (var note in recipe.Notes)
        {
            AssignId(appState, note.Id, value => note.Id = value, ref maxId);
            note.RecipeId = recipe.Id;
            note.Recipe = null;
        }

        foreach (var metadata in recipe.PlanningMetadata)
        {
            AssignId(appState, metadata.Id, value => metadata.Id = value, ref maxId);
            metadata.RecipeId = recipe.Id;
            metadata.Recipe = null;
        }

        foreach (var tag in recipe.Tags)
        {
            AssignId(appState, tag.Id, value => tag.Id = value, ref maxId);
            tag.RecipeId = recipe.Id;
            tag.Recipe = null;
        }

        foreach (var session in recipe.CookingHistory)
        {
            AssignId(appState, session.Id, value => session.Id = value, ref maxId);
            session.RecipeId = recipe.Id;
            session.Recipe = null;
        }
    }

    private static void EnsureCollections(LocalAppState appState)
    {
        appState.Recipes ??= [];
        appState.Ingredients ??= [];
        appState.Products ??= [];
        appState.MealPlanSlots ??= [];
        appState.PantryItems ??= [];
        appState.RecipeIdeas ??= [];
        appState.ProductBacklogItems ??= [];
        appState.ShoppingLists ??= [];

        foreach (var slot in appState.MealPlanSlots)
        {
            slot.PlannedRecipes ??= [];
        }

        foreach (var list in appState.ShoppingLists)
        {
            list.Items ??= [];
        }
    }

    private static void EnsureRecipeCollections(Recipe recipe)
    {
        recipe.Ingredients ??= [];
        recipe.Steps ??= [];
        recipe.Notes ??= [];
        recipe.PlanningMetadata ??= [];
        recipe.Tags ??= [];
        recipe.CookingHistory ??= [];

        foreach (var step in recipe.Steps)
        {
            step.IngredientReferences ??= [];
        }
    }

    private void AssignId(LocalAppState appState, int currentId, Action<int> assign, ref int maxId)
    {
        if (currentId <= 0)
        {
            currentId = NextId(appState);
            assign(currentId);
        }

        maxId = Math.Max(maxId, currentId);
    }

    private static string NormalizeScalingMode(string scalingMode) =>
        scalingMode switch
        {
            IngredientScalingMode.Fixed => IngredientScalingMode.Fixed,
            IngredientScalingMode.Approximate => IngredientScalingMode.Approximate,
            IngredientScalingMode.ToTaste => IngredientScalingMode.ToTaste,
            _ => IngredientScalingMode.Linear
        };

    private Ingredient ResolveRecipeIngredient(LocalAppState appState, RecipeIngredient recipeIngredient, ref int maxId)
    {
        var name = (recipeIngredient.Ingredient?.Name ?? recipeIngredient.Name).Trim();

        if (recipeIngredient.IngredientId is int ingredientId)
        {
            var existingById = FindIngredient(appState, ingredientId);
            if (existingById is not null)
            {
                var knownMatch = FindIngredientByKnowledgeMatch(appState, name);
                if (knownMatch is not null
                    && knownMatch.Id != existingById.Id
                    && string.IsNullOrWhiteSpace(existingById.KnowledgeId))
                {
                    return knownMatch;
                }

                return existingById;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Ingredient sense nom";
        }

        var existing = FindIngredientByKnowledgeMatch(appState, name);
        if (existing is not null)
        {
            return existing;
        }

        var ingredient = new Ingredient
        {
            Id = NextId(appState),
            Name = name,
            Category = GuessIngredientCategory(name),
            DefaultUnit = DefaultUnitFor(recipeIngredient.Unit),
            PantryCategory = GuessPantryCategory(name),
            CanFreeze = GuessCanFreeze(name)
        };
        EnrichIngredientNutrition(ingredient);

        appState.Ingredients.Add(ingredient);
        maxId = Math.Max(maxId, ingredient.Id);
        return ingredient;
    }

    private static void EnrichIngredientNutrition(Ingredient ingredient)
    {
        if (ingredient.NutritionPer100Grams?.HasAnyValue == true)
        {
            return;
        }

        var nutrition = IngredientNutritionKnowledge.FindForName(ingredient.Name);
        if (nutrition is null)
        {
            return;
        }

        ingredient.NutritionPer100Grams = nutrition;
        ingredient.NutritionSource = string.IsNullOrWhiteSpace(ingredient.NutritionSource)
            ? "Local approximate"
            : ingredient.NutritionSource;
    }

    private static Ingredient CloneIngredient(Ingredient ingredient) => new()
    {
        Id = ingredient.Id,
        KnowledgeId = ingredient.KnowledgeId,
        Name = ingredient.Name,
        Aliases = ingredient.Aliases.ToList(),
        Category = ingredient.Category,
        DefaultUnit = ingredient.DefaultUnit,
        PantryCategory = ingredient.PantryCategory,
        CanFreeze = ingredient.CanFreeze,
        Seasonality = ingredient.Seasonality,
        NutritionPer100Grams = CloneNutrition(ingredient.NutritionPer100Grams),
        NutritionSource = ingredient.NutritionSource
    };

    private static IngredientNutrition? CloneNutrition(IngredientNutrition? nutrition) => nutrition is null
        ? null
        : new IngredientNutrition
        {
            CaloriesKcal = nutrition.CaloriesKcal,
            ProteinGrams = nutrition.ProteinGrams,
            CarbohydrateGrams = nutrition.CarbohydrateGrams,
            FatGrams = nutrition.FatGrams,
            FibreGrams = nutrition.FibreGrams,
            SugarGrams = nutrition.SugarGrams,
            SaltGrams = nutrition.SaltGrams
        };

    private static Ingredient CreateIngredientSnapshot(Ingredient ingredient) => CloneIngredient(ingredient);

    private async Task MergeIngredientKnowledgeAsync(LocalAppState appState)
    {
        var knowledge = await LoadIngredientKnowledgeAsync();
        if (knowledge.Items.Count == 0)
        {
            return;
        }

        EnsureCollections(appState);
        foreach (var item in knowledge.Items.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var ingredient = FindIngredientForKnowledgeItem(appState, item);
            if (ingredient is null)
            {
                ingredient = new Ingredient
                {
                    Id = NextId(appState),
                    Name = item.Name.Trim()
                };
                appState.Ingredients.Add(ingredient);
            }

            ApplyKnowledge(ingredient, item);
        }
    }

    private async Task<IngredientKnowledgeFile> LoadIngredientKnowledgeAsync()
    {
        if (ingredientKnowledge is not null)
        {
            return ingredientKnowledge;
        }

        try
        {
            ingredientKnowledge = await httpClient.GetFromJsonAsync<IngredientKnowledgeFile>(
                "data/ingredients.json",
                JsonOptions)
                ?? new IngredientKnowledgeFile();
        }
        catch
        {
            ingredientKnowledge = new IngredientKnowledgeFile();
        }

        ingredientKnowledge.Items ??= [];
        return ingredientKnowledge;
    }

    private static Ingredient? FindIngredientForKnowledgeItem(LocalAppState appState, IngredientKnowledgeItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            var existingByKnowledgeId = appState.Ingredients.FirstOrDefault(ingredient =>
                string.Equals(ingredient.KnowledgeId, item.Id, StringComparison.OrdinalIgnoreCase));
            if (existingByKnowledgeId is not null)
            {
                return existingByKnowledgeId;
            }
        }

        var knowledgeKeys = KnowledgeKeysFor(item).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return appState.Ingredients.FirstOrDefault(ingredient =>
            IngredientKeysFor(ingredient).Any(knowledgeKeys.Contains));
    }

    private static Ingredient? FindIngredientByKnowledgeMatch(LocalAppState appState, string name)
    {
        var normalizedName = IngredientNameNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        return appState.Ingredients.FirstOrDefault(ingredient =>
            IngredientKeysFor(ingredient).Any(key => key == normalizedName));
    }

    private static IEnumerable<string> IngredientKeysFor(Ingredient ingredient)
    {
        var name = IngredientNameNormalizer.Normalize(ingredient.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name;
        }

        foreach (var alias in ingredient.Aliases ?? Enumerable.Empty<string>())
        {
            var normalized = IngredientNameNormalizer.Normalize(alias);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static IEnumerable<string> KnowledgeKeysFor(IngredientKnowledgeItem item)
    {
        var name = IngredientNameNormalizer.Normalize(item.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name;
        }

        foreach (var alias in item.Aliases ?? Enumerable.Empty<string>())
        {
            var normalized = IngredientNameNormalizer.Normalize(alias);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static void ApplyKnowledge(Ingredient ingredient, IngredientKnowledgeItem item)
    {
        ingredient.KnowledgeId = item.Id.Trim();
        if (string.IsNullOrWhiteSpace(ingredient.Name))
        {
            ingredient.Name = item.Name.Trim();
        }

        ingredient.Aliases = ingredient.Aliases
            .Concat(item.Aliases ?? Enumerable.Empty<string>())
            .Append(item.Name)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias)
            .ToList();

        ingredient.Category = MapKnowledgeIngredientCategory(item.Category);
        ingredient.DefaultUnit = string.IsNullOrWhiteSpace(item.DefaultUnit)
            ? ingredient.DefaultUnit
            : item.DefaultUnit.Trim();
        ingredient.PantryCategory = MapKnowledgeShoppingCategory(item.PantryCategory, item.Category);
        ingredient.CanFreeze = item.CanFreeze;

        if (item.Nutrition is not null)
        {
            ingredient.NutritionPer100Grams = new IngredientNutrition
            {
                CaloriesKcal = item.Nutrition.Calories,
                ProteinGrams = item.Nutrition.Protein,
                CarbohydrateGrams = item.Nutrition.Carbohydrates,
                FatGrams = item.Nutrition.Fat,
                FibreGrams = item.Nutrition.Fibre,
                SugarGrams = item.Nutrition.Sugar,
                SaltGrams = item.Nutrition.Salt
            };
            ingredient.NutritionSource = string.IsNullOrWhiteSpace(item.Source)
                ? "Nasdanus Knowledge"
                : item.Source.Trim();
        }
    }

    private static Product CreateProductSnapshot(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Brand = product.Brand,
        IngredientId = product.IngredientId,
        Barcode = product.Barcode,
        DefaultUnit = product.DefaultUnit,
        NutritionPer100Grams = CloneNutrition(product.NutritionPer100Grams),
        NutritionSource = product.NutritionSource
    };

    private static string NormalizeIngredientCategory(string category) =>
        IngredientCategory.All.Contains(category)
            ? category
            : IngredientCategory.Other;

    private static string NormalizeShoppingCategory(string category) =>
        ShoppingCategory.DisplayOrder.Contains(category)
            ? category
            : ShoppingCategory.Other;

    private static string MapKnowledgeIngredientCategory(string category) =>
        category switch
        {
            "vegetables" => IngredientCategory.Vegetables,
            "fruit" => IngredientCategory.Fruit,
            "meat" => IngredientCategory.Meat,
            "fish" => IngredientCategory.Fish,
            "dairy-eggs" => IngredientCategory.DairyEggs,
            "legumes" => IngredientCategory.Legumes,
            "grains" => IngredientCategory.Grains,
            "pantry" => IngredientCategory.Pantry,
            "spices" => IngredientCategory.Spices,
            _ => IngredientCategory.Other
        };

    private static string MapKnowledgeShoppingCategory(string pantryCategory, string ingredientCategory)
    {
        var category = string.IsNullOrWhiteSpace(pantryCategory) || pantryCategory == "other"
            ? ingredientCategory
            : pantryCategory;

        return category switch
        {
            "vegetables" or "fruit" => ShoppingCategory.Vegetables,
            "meat" => ShoppingCategory.Meat,
            "fish" => ShoppingCategory.Fish,
            "dairy-eggs" => ShoppingCategory.DairyEggs,
            "spices" => ShoppingCategory.Spices,
            "legumes" or "grains" or "pantry" => ShoppingCategory.Pantry,
            _ => ShoppingCategory.Other
        };
    }

    private static string DefaultUnitFor(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "kg" => "g",
            "grams" => "g",
            "gram" => "g",
            "gr" => "g",
            "ml" => "ml",
            "l" => "ml",
            _ => string.IsNullOrWhiteSpace(normalized) ? "g" : normalized
        };
    }

    private static string GuessIngredientCategory(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (ContainsAny(normalized, "tom", "ceba", "pastanaga", "carbass", "patata", "espinac", "pebrot", "enciam", "alberg"))
        {
            return IngredientCategory.Vegetables;
        }

        if (ContainsAny(normalized, "poma", "pera", "llimona", "taronja", "platan", "maduixa"))
        {
            return IngredientCategory.Fruit;
        }

        if (ContainsAny(normalized, "pollastre", "vedella", "porc", "gall dindi", "carn"))
        {
            return IngredientCategory.Meat;
        }

        if (ContainsAny(normalized, "salm", "tonyina", "bacall", "peix", "gamba", "muscl"))
        {
            return IngredientCategory.Fish;
        }

        if (ContainsAny(normalized, "ou", "llet", "iogurt", "formatge", "mantega", "nata"))
        {
            return IngredientCategory.DairyEggs;
        }

        if (ContainsAny(normalized, "cigr", "llent", "mongeta", "fesol"))
        {
            return IngredientCategory.Legumes;
        }

        if (ContainsAny(normalized, "arr", "pasta", "farina", "pa", "cous", "blat"))
        {
            return IngredientCategory.Grains;
        }

        if (ContainsAny(normalized, "sal", "pebre", "curc", "gingebre", "julivert", "herba", "xili", "bitxo"))
        {
            return IngredientCategory.Spices;
        }

        if (ContainsAny(normalized, "oli", "vinagre", "soja", "sucre", "mel", "llevat", "brou"))
        {
            return IngredientCategory.Pantry;
        }

        return IngredientCategory.Other;
    }

    private static string GuessPantryCategory(string name)
    {
        var category = GuessIngredientCategory(name);
        return category switch
        {
            IngredientCategory.Vegetables => ShoppingCategory.Vegetables,
            IngredientCategory.Meat => ShoppingCategory.Meat,
            IngredientCategory.Fish => ShoppingCategory.Fish,
            IngredientCategory.DairyEggs => ShoppingCategory.DairyEggs,
            IngredientCategory.Spices => ShoppingCategory.Spices,
            IngredientCategory.Grains or IngredientCategory.Pantry or IngredientCategory.Legumes => ShoppingCategory.Pantry,
            _ => ShoppingCategory.Other
        };
    }

    private static bool GuessCanFreeze(string name)
    {
        var normalized = name.ToLowerInvariant();
        return ContainsAny(normalized, "pollastre", "vedella", "porc", "peix", "salm", "bacall", "gamba", "brou", "pa");
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(value.Contains);

    private static LocalAppState? DeserializeStoredState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LocalAppState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LocalAppState? DeserializeImportState(string json, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var backup = JsonSerializer.Deserialize<NasdanusBackupFile>(json, JsonOptions);
            if (backup?.Data is not null)
            {
                if (!string.Equals(backup.Application, BackupApplicationName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("La copia no identifica correctament l'aplicacio Nasdanus.");
                }

                return backup.Data;
            }
        }
        catch (JsonException)
        {
            // Try the legacy raw state shape below.
        }

        try
        {
            return JsonSerializer.Deserialize<LocalAppState>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ValidateState(LocalAppState appState)
    {
        var errors = new List<string>();
        if (appState.SchemaVersion <= 0)
        {
            errors.Add("La versio de dades no es valida.");
        }

        if (appState.Recipes.Count == 0)
        {
            errors.Add("La copia no conte cap recepta.");
        }

        var recipeIds = appState.Recipes.Select(recipe => recipe.Id).ToList();
        var recipeIdSet = recipeIds.ToHashSet();
        var ingredientIds = appState.Ingredients.Select(ingredient => ingredient.Id).ToHashSet();
        if (recipeIds.Count != recipeIdSet.Count)
        {
            errors.Add("La copia conte receptes amb identificadors duplicats.");
        }

        foreach (var recipe in appState.Recipes)
        {
            if (recipe.Id <= 0)
            {
                errors.Add("Hi ha una recepta sense identificador valid.");
            }

            if (string.IsNullOrWhiteSpace(recipe.Name))
            {
                errors.Add($"La recepta {recipe.Id} no te nom.");
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.IngredientId is int ingredientId && !ingredientIds.Contains(ingredientId))
                {
                    errors.Add($"La recepta {recipe.Id} referencia un ingredient inexistent ({ingredientId}).");
                }
            }
        }

        foreach (var product in appState.Products)
        {
            if (product.IngredientId is int ingredientId && !ingredientIds.Contains(ingredientId))
            {
                errors.Add($"El producte {product.Id} referencia un ingredient inexistent ({ingredientId}).");
            }
        }

        foreach (var plannedRecipe in appState.MealPlanSlots.SelectMany(slot => slot.PlannedRecipes))
        {
            if (!recipeIdSet.Contains(plannedRecipe.RecipeId))
            {
                errors.Add($"El planner referencia una recepta inexistent ({plannedRecipe.RecipeId}).");
            }
        }

        foreach (var idea in appState.RecipeIdeas)
        {
            if (!recipeIdSet.Contains(idea.RecipeId))
            {
                errors.Add($"Recipe Ideas referencia una recepta inexistent ({idea.RecipeId}).");
            }
        }

        foreach (var item in appState.ShoppingLists.SelectMany(list => list.Items))
        {
            if (item.RecipeId is int recipeId && !recipeIdSet.Contains(recipeId))
            {
                errors.Add($"La llista de la compra referencia una recepta inexistent ({recipeId}).");
            }
        }

        return errors.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static ProductBacklogItem CreateProductBacklogSnapshot(ProductBacklogItem item) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Scope = item.Scope,
        Title = item.Title,
        Description = item.Description,
        Priority = item.Priority,
        Status = item.Status,
        DuplicateOfId = item.DuplicateOfId,
        Labels = item.Labels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label)
            .ToList(),
        ApplicationVersion = item.ApplicationVersion,
        TargetVersion = item.TargetVersion,
        Decision = item.Decision,
        ResolutionNotes = item.ResolutionNotes,
        Context = new ProductBacklogContext
        {
            FeedbackId = item.Context?.FeedbackId ?? item.Id,
            Page = item.Context?.Page ?? string.Empty,
            CurrentUrl = item.Context?.CurrentUrl ?? string.Empty,
            CapturedAt = item.Context?.CapturedAt ?? item.CreatedAt,
            BrowserInformation = item.Context?.BrowserInformation ?? string.Empty,
            RecipeId = item.Context?.RecipeId,
            RecipeName = item.Context?.RecipeName ?? string.Empty,
            PlannerWeek = item.Context?.PlannerWeek,
            PlannerDay = item.Context?.PlannerDay,
            Meal = item.Context?.Meal ?? string.Empty,
            CookingStepNumber = item.Context?.CookingStepNumber,
            ShoppingWeek = item.Context?.ShoppingWeek,
            ShoppingCategory = item.Context?.ShoppingCategory ?? string.Empty,
            PantryItemId = item.Context?.PantryItemId,
            PantryItemName = item.Context?.PantryItemName ?? string.Empty
        },
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        ClosedAt = item.ClosedAt
    };

    private sealed class IngredientKnowledgeFile
    {
        public int SchemaVersion { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public string Generator { get; set; } = string.Empty;
        public List<IngredientKnowledgeItem> Items { get; set; } = [];
    }

    private sealed class IngredientKnowledgeItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = [];
        public string Category { get; set; } = string.Empty;
        public string DefaultUnit { get; set; } = string.Empty;
        public bool CanFreeze { get; set; }
        public string PantryCategory { get; set; } = string.Empty;
        public IngredientKnowledgeNutrition? Nutrition { get; set; }
        public string Source { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
    }

    private sealed class IngredientKnowledgeNutrition
    {
        public decimal? Calories { get; set; }
        public decimal? Protein { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Fat { get; set; }
        public decimal? Fibre { get; set; }
        public decimal? Sugar { get; set; }
        public decimal? Salt { get; set; }
    }

    private static LocalAppState CreateFallbackState()
    {
        var recipe = new Recipe
        {
            Id = 1,
            Name = "Salmó teriyaki",
            Description = "Recepta base per provar Nasdanus en mode estàtic.",
            Category = "Sopar",
            Status = RecipeStatus.Active,
            PreparationTimeMinutes = 10,
            CookingTimeMinutes = 15,
            Difficulty = 2,
            Servings = 3
        };
        recipe.Ingredients =
        [
            new RecipeIngredient { Id = 2, RecipeId = 1, Order = 1, Name = "Salmó", Quantity = "450", Unit = "g" },
            new RecipeIngredient { Id = 3, RecipeId = 1, Order = 2, Name = "Salsa de soja", Quantity = "3", Unit = "cullerades", ScalingMode = IngredientScalingMode.Approximate },
            new RecipeIngredient { Id = 4, RecipeId = 1, Order = 3, Name = "Arròs", Quantity = "240", Unit = "g" }
        ];
        recipe.Steps =
        [
            new RecipeStep { Id = 5, RecipeId = 1, Order = 1, Title = "Pas 1", Instruction = "Barreja la salsa i marina el salmó." },
            new RecipeStep { Id = 6, RecipeId = 1, Order = 2, Title = "Pas 2", Instruction = "Cuina el salmó i serveix-lo amb arròs.", TimerMinutes = 12 }
        ];

        return new LocalAppState
        {
            NextId = 1000,
            Recipes = [recipe],
            PantryItems =
            [
                new PantryItem { Id = 7, Name = "Oli d'oliva", Category = ShoppingCategory.Pantry },
                new PantryItem { Id = 8, Name = "Sal", Category = ShoppingCategory.Spices }
            ]
        };
    }
}

public sealed class LocalAppState
{
    public int SchemaVersion { get; set; } = 1;
    public int NextId { get; set; } = 1000;
    public List<Ingredient> Ingredients { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Recipe> Recipes { get; set; } = [];
    public List<MealPlanSlot> MealPlanSlots { get; set; } = [];
    public List<PantryItem> PantryItems { get; set; } = [];
    public List<RecipeIdea> RecipeIdeas { get; set; } = [];
    public List<ProductBacklogItem> ProductBacklogItems { get; set; } = [];
    public List<ShoppingList> ShoppingLists { get; set; } = [];
}

public sealed class NasdanusBackupFile
{
    public string Application { get; set; } = "Nasdanus";
    public int BackupFormatVersion { get; set; } = 1;
    public int SchemaVersion { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public DataBackupSummary Summary { get; set; } = new();
    public LocalAppState? Data { get; set; }
}

public sealed class DataBackupSummary
{
    public int Recipes { get; set; }
    public int DraftRecipes { get; set; }
    public int Ingredients { get; set; }
    public int Products { get; set; }
    public int MealPlanSlots { get; set; }
    public int PlannedRecipes { get; set; }
    public int ShoppingLists { get; set; }
    public int ShoppingItems { get; set; }
    public int PantryItems { get; set; }
    public int RecipeIdeas { get; set; }
    public int ProductBacklogItems { get; set; }

    public static DataBackupSummary From(LocalAppState state) => new()
    {
        Recipes = state.Recipes.Count,
        DraftRecipes = state.Recipes.Count(recipe => recipe.IsDraft),
        Ingredients = state.Ingredients.Count,
        Products = state.Products.Count,
        MealPlanSlots = state.MealPlanSlots.Count,
        PlannedRecipes = state.MealPlanSlots.Sum(slot => slot.PlannedRecipes.Count),
        ShoppingLists = state.ShoppingLists.Count,
        ShoppingItems = state.ShoppingLists.Sum(list => list.Items.Count),
        PantryItems = state.PantryItems.Count,
        RecipeIdeas = state.RecipeIdeas.Count,
        ProductBacklogItems = state.ProductBacklogItems.Count
    };
}

public sealed class DataImportValidationResult
{
    public bool IsValid { get; init; }
    public DataBackupSummary? Summary { get; init; }
    public List<string> Errors { get; init; } = [];
    public LocalAppState? State { get; init; }

    public static DataImportValidationResult Valid(LocalAppState state, DataBackupSummary summary) => new()
    {
        IsValid = true,
        Summary = summary,
        State = state
    };

    public static DataImportValidationResult Invalid(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
