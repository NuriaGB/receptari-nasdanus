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

    public async Task<LocalAppState> GetStateAsync()
    {
        if (state is not null)
        {
            return state;
        }

        state = await LoadStoredStateAsync();

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
        await SaveAsync();
        return validation;
    }

    public int NextId(LocalAppState appState) => appState.NextId++;

    public Recipe? FindRecipe(LocalAppState appState, int recipeId) =>
        appState.Recipes.FirstOrDefault(recipe => recipe.Id == recipeId);

    public Recipe CloneRecipe(Recipe recipe)
    {
        var ingredients = recipe.Ingredients
            .OrderBy(ingredient => ingredient.Order)
            .Select(ingredient => new RecipeIngredient
            {
                Id = ingredient.Id,
                RecipeId = recipe.Id,
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
