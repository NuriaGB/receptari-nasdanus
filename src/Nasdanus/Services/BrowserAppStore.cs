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
    private HouseholdIngredientPreferenceSeedFile? householdIngredientPreferenceSeed;

    public async Task<LocalAppState> GetStateAsync()
    {
        if (state is not null)
        {
            return state;
        }

        state = await LoadStoredStateAsync();

        Normalize(state);
        await MergeIngredientKnowledgeAsync(state);
        await MergeHouseholdIngredientPreferenceSeedAsync(state);
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
        await MergeHouseholdIngredientPreferenceSeedAsync(state);
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
            ImageUrl = recipe.ImageUrl,
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
        PlanningSettings = ClonePlanningSettings(source.PlanningSettings),
        HouseholdIngredientPreferences = source.HouseholdIngredientPreferences
            .Select(CloneHouseholdIngredientPreference)
            .ToList(),
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
            ImageUrl = recipe.ImageUrl,
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
            ingredient.CatalanName = ingredient.CatalanName.Trim();
            ingredient.SpanishName = ingredient.SpanishName.Trim();
            ingredient.Aliases ??= [];
            ingredient.Aliases = ingredient.Aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(alias => alias.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias)
                .ToList();
            ingredient.Category = NormalizeIngredientCategory(ingredient.Category);
            ingredient.Subcategory = ingredient.Subcategory.Trim();
            ingredient.NutritionState = NormalizeNutritionState(ingredient.NutritionState);
            ingredient.PantryCategory = NormalizeShoppingCategory(ingredient.PantryCategory);
            ingredient.NutritionSource = ingredient.NutritionSource.Trim();
            ingredient.NutritionSourceId = ingredient.NutritionSourceId.Trim();
            EnrichIngredientNutrition(ingredient);
        }

        appState.HouseholdIngredientPreferences = appState.HouseholdIngredientPreferences
            .Select(NormalizeHouseholdIngredientPreference)
            .Where(preference => !string.IsNullOrWhiteSpace(preference.IngredientKnowledgeId))
            .GroupBy(preference => preference.IngredientKnowledgeId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(preference => preference.IngredientKnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
            recipe.ImageUrl = recipe.ImageUrl?.Trim() ?? string.Empty;
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
        appState.HouseholdIngredientPreferences ??= [];
        appState.PlanningSettings ??= new HouseholdPlanningSettings();
        NormalizePlanningSettings(appState.PlanningSettings);

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
        CatalanName = ingredient.CatalanName,
        SpanishName = ingredient.SpanishName,
        Aliases = ingredient.Aliases.ToList(),
        Category = ingredient.Category,
        Subcategory = ingredient.Subcategory,
        DefaultUnit = ingredient.DefaultUnit,
        PantryCategory = ingredient.PantryCategory,
        CanFreeze = ingredient.CanFreeze,
        Seasonality = ingredient.Seasonality,
        NutritionPer100Grams = CloneNutrition(ingredient.NutritionPer100Grams),
        NutritionState = ingredient.NutritionState,
        NutritionSource = ingredient.NutritionSource,
        NutritionSourceId = ingredient.NutritionSourceId,
        NutritionLastUpdated = ingredient.NutritionLastUpdated
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

    private async Task MergeHouseholdIngredientPreferenceSeedAsync(LocalAppState appState)
    {
        var preferenceSeed = await LoadHouseholdIngredientPreferenceSeedAsync();
        if (preferenceSeed.Items.Count == 0)
        {
            return;
        }

        EnsureCollections(appState);
        var existingKnowledgeIds = appState.HouseholdIngredientPreferences
            .Where(preference => !string.IsNullOrWhiteSpace(preference.IngredientKnowledgeId))
            .Select(preference => preference.IngredientKnowledgeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in preferenceSeed.Items)
        {
            var ingredient = FindIngredientForPreferenceSeed(appState, item);
            if (ingredient is null
                || string.IsNullOrWhiteSpace(ingredient.KnowledgeId)
                || existingKnowledgeIds.Contains(ingredient.KnowledgeId))
            {
                continue;
            }

            appState.HouseholdIngredientPreferences.Add(new HouseholdIngredientPreference
            {
                IngredientKnowledgeId = ingredient.KnowledgeId,
                IsFrequentlyUsed = item.IsFrequentlyUsed,
                IsUsuallyAvailable = item.IsUsuallyAvailable,
                UseFrequency = IngredientUseFrequency.All.Contains(item.UseFrequency)
                    ? item.UseFrequency
                    : IngredientUseFrequency.Frequent,
                PreferredAlias = item.PreferredAlias.Trim(),
                HouseholdNotes = item.HouseholdNotes.Trim()
            });
            existingKnowledgeIds.Add(ingredient.KnowledgeId);
        }
    }

    private async Task<HouseholdIngredientPreferenceSeedFile> LoadHouseholdIngredientPreferenceSeedAsync()
    {
        if (householdIngredientPreferenceSeed is not null)
        {
            return householdIngredientPreferenceSeed;
        }

        try
        {
            householdIngredientPreferenceSeed = await httpClient.GetFromJsonAsync<HouseholdIngredientPreferenceSeedFile>(
                "data/household-ingredient-preferences.json",
                JsonOptions)
                ?? new HouseholdIngredientPreferenceSeedFile();
        }
        catch
        {
            householdIngredientPreferenceSeed = new HouseholdIngredientPreferenceSeedFile();
        }

        householdIngredientPreferenceSeed.Items ??= [];
        return householdIngredientPreferenceSeed;
    }

    private static Ingredient? FindIngredientForPreferenceSeed(
        LocalAppState appState,
        HouseholdIngredientPreferenceSeedItem item)
    {
        var matchKeys = item.MatchNames
            .Append(item.PreferredAlias)
            .Select(IngredientNameNormalizer.Normalize)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matchKeys.Count == 0)
        {
            return null;
        }

        return appState.Ingredients
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.KnowledgeId))
            .FirstOrDefault(ingredient =>
            {
                var ingredientKeys = IngredientKeysFor(ingredient).ToList();
                return matchKeys.Any(matchKey => ingredientKeys.Any(key => IsIngredientNameMatch(matchKey, key)));
            });
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
            IngredientKeysFor(ingredient).Any(key => IsIngredientNameMatch(normalizedName, key)));
    }

    private static bool IsIngredientNameMatch(string normalizedName, string normalizedKnownKey)
    {
        if (string.Equals(normalizedName, normalizedKnownKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedKnownKey) || normalizedKnownKey.Length < 5)
        {
            return false;
        }

        return $" {normalizedName} ".Contains($" {normalizedKnownKey} ", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> IngredientKeysFor(Ingredient ingredient)
    {
        var name = IngredientNameNormalizer.Normalize(ingredient.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name;
        }

        var catalanName = IngredientNameNormalizer.Normalize(ingredient.CatalanName);
        if (!string.IsNullOrWhiteSpace(catalanName))
        {
            yield return catalanName;
        }

        var spanishName = IngredientNameNormalizer.Normalize(ingredient.SpanishName);
        if (!string.IsNullOrWhiteSpace(spanishName))
        {
            yield return spanishName;
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

        var catalanName = IngredientNameNormalizer.Normalize(item.CatalanName);
        if (!string.IsNullOrWhiteSpace(catalanName))
        {
            yield return catalanName;
        }

        var spanishName = IngredientNameNormalizer.Normalize(item.SpanishName);
        if (!string.IsNullOrWhiteSpace(spanishName))
        {
            yield return spanishName;
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
        ingredient.Name = item.Name.Trim();
        ingredient.CatalanName = item.CatalanName.Trim();
        ingredient.SpanishName = item.SpanishName.Trim();
        ingredient.Aliases = ingredient.Aliases
            .Concat(item.Aliases ?? Enumerable.Empty<string>())
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias)
            .ToList();

        ingredient.Category = MapKnowledgeIngredientCategory(item.Category);
        ingredient.Subcategory = item.Subcategory.Trim();
        ingredient.DefaultUnit = string.IsNullOrWhiteSpace(item.DefaultUnit)
            ? ingredient.DefaultUnit
            : item.DefaultUnit.Trim();
        ingredient.PantryCategory = MapKnowledgeShoppingCategory(item.PantryCategory, item.Category);
        ingredient.CanFreeze = item.CanFreeze;
        var hasManualNutrition = HasManualNutrition(ingredient);
        if (!hasManualNutrition)
        {
            ingredient.NutritionState = NormalizeNutritionState(item.NutritionState);
        }

        if (item.Nutrition is not null && !hasManualNutrition)
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
            ingredient.NutritionSourceId = item.SourceId.Trim();
            ingredient.NutritionLastUpdated = item.LastUpdated;
        }
    }

    private static bool HasManualNutrition(Ingredient ingredient) =>
        string.Equals(ingredient.NutritionSource, "manual", StringComparison.OrdinalIgnoreCase)
        && ingredient.NutritionPer100Grams?.HasAnyValue == true;

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

    private static HouseholdIngredientPreference CloneHouseholdIngredientPreference(
        HouseholdIngredientPreference preference) => new()
    {
        IngredientKnowledgeId = preference.IngredientKnowledgeId,
        IsFavourite = preference.IsFavourite,
        IsFrequentlyUsed = preference.IsFrequentlyUsed,
        IsUsuallyAvailable = preference.IsUsuallyAvailable,
        IsAlwaysInPantry = preference.IsAlwaysInPantry,
        IsNormallyFrozen = preference.IsNormallyFrozen,
        UseFrequency = preference.UseFrequency,
        PreferredAlias = preference.PreferredAlias,
        HouseholdNotes = preference.HouseholdNotes
    };

    private static HouseholdIngredientPreference NormalizeHouseholdIngredientPreference(
        HouseholdIngredientPreference preference)
    {
        var normalized = CloneHouseholdIngredientPreference(preference);
        normalized.IngredientKnowledgeId = normalized.IngredientKnowledgeId.Trim();
        normalized.UseFrequency = IngredientUseFrequency.All.Contains(normalized.UseFrequency)
            ? normalized.UseFrequency
            : IngredientUseFrequency.Occasional;
        normalized.PreferredAlias = normalized.PreferredAlias.Trim();
        normalized.HouseholdNotes = normalized.HouseholdNotes.Trim();
        return normalized;
    }

    private static HouseholdPlanningSettings ClonePlanningSettings(HouseholdPlanningSettings? settings)
    {
        var clone = new HouseholdPlanningSettings
        {
            General = new HouseholdGeneralSettings
            {
                HouseholdName = settings?.General?.HouseholdName ?? "Nasdanus",
                DefaultLanguage = settings?.General?.DefaultLanguage ?? HouseholdLanguage.Catalan,
                MeasurementSystem = settings?.General?.MeasurementSystem ?? MeasurementSystemKind.Metric,
                DefaultServings = settings?.General?.DefaultServings ?? 4,
                WeekStartsOn = settings?.General?.WeekStartsOn ?? DayOfWeek.Monday
            },
            Members = settings?.Members?.Select(CloneHouseholdMemberProfile).ToList()
                ?? HouseholdMemberDefaults.Create(),
            NutritionGoals = new HouseholdNutritionGoals
            {
                GoalScope = settings?.NutritionGoals?.GoalScope ?? NutritionGoalScope.WeeklyAverage,
                MacroMode = settings?.NutritionGoals?.MacroMode ?? NutritionMacroMode.PercentageDistribution,
                TargetCaloriesPerPerson = settings?.NutritionGoals?.TargetCaloriesPerPerson ?? 2000,
                MinimumProteinGramsPerPerson = settings?.NutritionGoals?.MinimumProteinGramsPerPerson ?? 150,
                TargetCarbohydrateGramsPerPerson = settings?.NutritionGoals?.TargetCarbohydrateGramsPerPerson ?? 175,
                TargetFatGramsPerPerson = settings?.NutritionGoals?.TargetFatGramsPerPerson ?? 77.8m,
                TargetFibreGramsPerPerson = settings?.NutritionGoals?.TargetFibreGramsPerPerson ?? 30,
                ProteinPercent = settings?.NutritionGoals?.ProteinPercent ?? 30,
                CarbohydratePercent = settings?.NutritionGoals?.CarbohydratePercent ?? 35,
                FatPercent = settings?.NutritionGoals?.FatPercent ?? 35
            },
            WeeklyFoodRules = new WeeklyFoodRules
            {
                MinimumFishMeals = settings?.WeeklyFoodRules?.MinimumFishMeals ?? 2,
                MinimumLegumeMeals = settings?.WeeklyFoodRules?.MinimumLegumeMeals ?? 1,
                MaximumRedMeatMeals = settings?.WeeklyFoodRules?.MaximumRedMeatMeals ?? 1,
                MinimumVegetableRichMeals = settings?.WeeklyFoodRules?.MinimumVegetableRichMeals ?? 7,
                Targets = settings?.WeeklyFoodRules?.Targets
                    ?.Select(CloneWeeklyFoodTarget)
                    .ToList() ?? [],
                DayRules = settings?.WeeklyFoodRules?.DayRules
                    ?.Select(rule => new DayFoodRule
                    {
                        DayOfWeek = rule.DayOfWeek,
                        FoodGroup = rule.FoodGroup,
                        FoodGroups = rule.FoodGroups?.ToList() ?? []
                    })
                    .ToList() ?? []
            },
            CookingPreferences = new HouseholdCookingPreferences
            {
                MaximumWeekdayCookingMinutes = settings?.CookingPreferences?.MaximumWeekdayCookingMinutes ?? 45,
                MaximumWeekendCookingMinutes = settings?.CookingPreferences?.MaximumWeekendCookingMinutes ?? 90,
                UseFreezerMeals = settings?.CookingPreferences?.UseFreezerMeals ?? true,
                PreferSeasonalIngredients = settings?.CookingPreferences?.PreferSeasonalIngredients ?? true,
                PreferLocalIngredients = settings?.CookingPreferences?.PreferLocalIngredients ?? true,
                AvoidRepeatingRecipesWithinDays = settings?.CookingPreferences?.AvoidRepeatingRecipesWithinDays ?? 10,
                PreferFavouriteRecipes = settings?.CookingPreferences?.PreferFavouriteRecipes ?? true,
                PreferSuccessfullyCookedRecipes = settings?.CookingPreferences?.PreferSuccessfullyCookedRecipes ?? true,
                AllowLeftovers = settings?.CookingPreferences?.AllowLeftovers ?? true,
                MinimumVarietyMealsPerWeek = settings?.CookingPreferences?.MinimumVarietyMealsPerWeek ?? 7,
                DesiredVarietyWindowDays = settings?.CookingPreferences?.DesiredVarietyWindowDays ?? 14,
                PrioritizeAvailableFreezerIngredients = settings?.CookingPreferences?.PrioritizeAvailableFreezerIngredients ?? true,
                PrioritizePantryIngredients = settings?.CookingPreferences?.PrioritizePantryIngredients ?? true,
                PreferBatchFriendlyPreparations = settings?.CookingPreferences?.PreferBatchFriendlyPreparations ?? true
            },
            KitchenPantry = new HouseholdKitchenPantrySettings
            {
                AlwaysAvailableIngredients = settings?.KitchenPantry?.AlwaysAvailableIngredients ?? string.Empty,
                FridgeInventoryNotes = settings?.KitchenPantry?.FridgeInventoryNotes ?? string.Empty,
                FreezerInventoryNotes = settings?.KitchenPantry?.FreezerInventoryNotes ?? string.Empty,
                PantryStaplesNotes = settings?.KitchenPantry?.PantryStaplesNotes ?? string.Empty,
                PreferredBrands = settings?.KitchenPantry?.PreferredBrands ?? string.Empty,
                FreezerItems = settings?.KitchenPantry?.FreezerItems?.Select(CloneFreezerInventoryItem).ToList() ?? [],
                Appliances = new KitchenApplianceSettings
                {
                    AirFryer = settings?.KitchenPantry?.Appliances?.AirFryer ?? false,
                    PressureCooker = settings?.KitchenPantry?.Appliances?.PressureCooker ?? false,
                    Oven = settings?.KitchenPantry?.Appliances?.Oven ?? true,
                    Bbq = settings?.KitchenPantry?.Appliances?.Bbq ?? false,
                    Thermomix = settings?.KitchenPantry?.Appliances?.Thermomix ?? false,
                    SteamCooker = settings?.KitchenPantry?.Appliances?.SteamCooker ?? false
                }
            },
            Shopping = new HouseholdShoppingSettings
            {
                MergeDuplicatedIngredients = settings?.Shopping?.MergeDuplicatedIngredients ?? true,
                SortBySupermarketOrder = settings?.Shopping?.SortBySupermarketOrder ?? true,
                IgnorePantryItems = settings?.Shopping?.IgnorePantryItems ?? true,
                IgnoreAlwaysAvailableIngredients = settings?.Shopping?.IgnoreAlwaysAvailableIngredients ?? true,
                DeductAvailableFreezerItems = settings?.Shopping?.DeductAvailableFreezerItems ?? false,
                AutomaticQuantityAggregation = settings?.Shopping?.AutomaticQuantityAggregation ?? true,
                PreferredUnits = settings?.Shopping?.PreferredUnits ?? PreferredUnitMode.RecipeUnits,
                DefaultFreshShoppingDay = settings?.Shopping?.DefaultFreshShoppingDay ?? DayOfWeek.Saturday,
                DefaultGeneralShoppingDay = settings?.Shopping?.DefaultGeneralShoppingDay ?? DayOfWeek.Saturday,
                PreserveManualItemsWhenRegenerating = settings?.Shopping?.PreserveManualItemsWhenRegenerating ?? true
            }
        };

        NormalizePlanningSettings(clone);
        return clone;
    }

    private static WeeklyFoodTarget CloneWeeklyFoodTarget(WeeklyFoodTarget target) => new()
    {
        FoodGroup = target.FoodGroup,
        RuleType = target.RuleType,
        MealsPerWeek = target.MealsPerWeek
    };

    private static HouseholdMemberProfile CloneHouseholdMemberProfile(HouseholdMemberProfile member) => new()
    {
        Id = member.Id,
        Name = member.Name,
        DateOfBirth = member.DateOfBirth,
        MeasurementDate = member.MeasurementDate,
        HeightCentimeters = member.HeightCentimeters,
        WeightKilograms = member.WeightKilograms,
        BodyFatPercentage = member.BodyFatPercentage,
        Sex = member.Sex,
        CurrentLifeStage = member.CurrentLifeStage,
        ActivityLevel = member.ActivityLevel,
        WeeklyExercise = member.WeeklyExercise,
        Occupation = member.Occupation,
        HealthNotes = member.HealthNotes,
        FavouriteFoods = member.FavouriteFoods,
        FoodsToAvoid = member.FoodsToAvoid,
        FoodsToEncourage = member.FoodsToEncourage,
        SpiceTolerance = member.SpiceTolerance,
        CookingPreferences = member.CookingPreferences,
        NutritionGoals = member.NutritionGoals.ToList(),
        CustomNutritionGoals = member.CustomNutritionGoals,
        MeasurementHistory = member.MeasurementHistory?.Select(CloneBodyMeasurement).ToList() ?? []
    };

    private static BodyMeasurement CloneBodyMeasurement(BodyMeasurement measurement) => new()
    {
        Id = measurement.Id,
        Date = measurement.Date,
        WeightKilograms = measurement.WeightKilograms,
        HeightCentimeters = measurement.HeightCentimeters,
        BodyFatPercentage = measurement.BodyFatPercentage,
        Notes = measurement.Notes
    };

    private static FreezerInventoryItem CloneFreezerInventoryItem(FreezerInventoryItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Quantity = item.Quantity,
        Unit = item.Unit,
        FrozenDate = item.FrozenDate,
        BestBeforeDate = item.BestBeforeDate,
        Notes = item.Notes
    };

    private static void NormalizePlanningSettings(HouseholdPlanningSettings settings)
    {
        settings.General ??= new HouseholdGeneralSettings();
        settings.NutritionGoals ??= new HouseholdNutritionGoals();
        settings.WeeklyFoodRules ??= new WeeklyFoodRules();
        settings.CookingPreferences ??= new HouseholdCookingPreferences();
        settings.KitchenPantry ??= new HouseholdKitchenPantrySettings();
        settings.KitchenPantry.Appliances ??= new KitchenApplianceSettings();
        settings.Shopping ??= new HouseholdShoppingSettings();

        settings.General.HouseholdName = string.IsNullOrWhiteSpace(settings.General.HouseholdName)
            ? "Nasdanus"
            : settings.General.HouseholdName.Trim();
        settings.General.DefaultLanguage = HouseholdLanguage.All.Contains(settings.General.DefaultLanguage)
            ? settings.General.DefaultLanguage
            : HouseholdLanguage.Catalan;
        settings.General.MeasurementSystem = MeasurementSystemKind.All.Contains(settings.General.MeasurementSystem)
            ? settings.General.MeasurementSystem
            : MeasurementSystemKind.Metric;
        settings.General.DefaultServings = Math.Clamp(settings.General.DefaultServings, 1, 20);

        settings.Members ??= HouseholdMemberDefaults.Create();
        settings.Members = settings.Members
            .Select(NormalizeHouseholdMemberProfile)
            .ToList();

        var usedLegacyMacroDefaults = settings.NutritionGoals.MacroMode == NutritionMacroMode.AbsoluteGrams
            && settings.NutritionGoals.ProteinPercent == 30
            && settings.NutritionGoals.CarbohydratePercent == 40
            && settings.NutritionGoals.FatPercent == 30;

        settings.NutritionGoals.GoalScope = NutritionGoalScope.All.Contains(settings.NutritionGoals.GoalScope)
            ? settings.NutritionGoals.GoalScope
            : NutritionGoalScope.WeeklyAverage;
        settings.NutritionGoals.MacroMode = NutritionMacroMode.PercentageDistribution;
        settings.NutritionGoals.TargetCaloriesPerPerson = Math.Max(0, settings.NutritionGoals.TargetCaloriesPerPerson);
        settings.NutritionGoals.TargetFibreGramsPerPerson = Math.Max(0, settings.NutritionGoals.TargetFibreGramsPerPerson);
        settings.NutritionGoals.ProteinPercent = Math.Clamp(settings.NutritionGoals.ProteinPercent, 0, 100);
        settings.NutritionGoals.CarbohydratePercent = Math.Clamp(settings.NutritionGoals.CarbohydratePercent, 0, 100);
        settings.NutritionGoals.FatPercent = Math.Clamp(settings.NutritionGoals.FatPercent, 0, 100);
        if (usedLegacyMacroDefaults)
        {
            settings.NutritionGoals.CarbohydratePercent = 35;
            settings.NutritionGoals.FatPercent = 35;
            settings.NutritionGoals.TargetFibreGramsPerPerson = 30;
        }

        SyncCalculatedMacroGramTargets(settings.NutritionGoals);

        settings.WeeklyFoodRules.MinimumFishMeals = Math.Max(0, settings.WeeklyFoodRules.MinimumFishMeals);
        settings.WeeklyFoodRules.MinimumLegumeMeals = Math.Max(0, settings.WeeklyFoodRules.MinimumLegumeMeals);
        settings.WeeklyFoodRules.MaximumRedMeatMeals = Math.Max(0, settings.WeeklyFoodRules.MaximumRedMeatMeals);
        settings.WeeklyFoodRules.MinimumVegetableRichMeals = Math.Max(0, settings.WeeklyFoodRules.MinimumVegetableRichMeals);
        settings.WeeklyFoodRules.Targets = NormalizeWeeklyFoodTargets(settings.WeeklyFoodRules);
        SyncLegacyWeeklyTargets(settings.WeeklyFoodRules);

        var dayRules = settings.WeeklyFoodRules.DayRules ?? [];
        settings.WeeklyFoodRules.DayRules = Enum.GetValues<DayOfWeek>()
            .OrderBy(PlannerDaySortOrder)
            .Select(day =>
            {
                var existing = dayRules.FirstOrDefault(rule => rule.DayOfWeek == day);
                var groups = NormalizeFoodGroups(existing?.FoodGroups ?? []);
                var fallbackGroup = NormalizeFoodGroup(existing?.FoodGroup ?? DefaultFoodGroupFor(day));
                if (groups.Count == 0 && !string.IsNullOrWhiteSpace(fallbackGroup))
                {
                    groups.Add(fallbackGroup);
                }

                return new DayFoodRule
                {
                    DayOfWeek = day,
                    FoodGroup = groups.FirstOrDefault() ?? FoodGroupKind.None,
                    FoodGroups = groups
                };
            })
            .ToList();

        settings.CookingPreferences.MaximumWeekdayCookingMinutes = Math.Clamp(settings.CookingPreferences.MaximumWeekdayCookingMinutes, 0, 240);
        settings.CookingPreferences.MaximumWeekendCookingMinutes = Math.Clamp(settings.CookingPreferences.MaximumWeekendCookingMinutes, 0, 360);
        settings.CookingPreferences.AvoidRepeatingRecipesWithinDays = Math.Clamp(settings.CookingPreferences.AvoidRepeatingRecipesWithinDays, 0, 90);
        settings.CookingPreferences.MinimumVarietyMealsPerWeek = Math.Clamp(settings.CookingPreferences.MinimumVarietyMealsPerWeek, 0, 14);
        settings.CookingPreferences.DesiredVarietyWindowDays = Math.Clamp(settings.CookingPreferences.DesiredVarietyWindowDays, 0, 90);

        settings.KitchenPantry.AlwaysAvailableIngredients = settings.KitchenPantry.AlwaysAvailableIngredients.Trim();
        settings.KitchenPantry.FridgeInventoryNotes = settings.KitchenPantry.FridgeInventoryNotes.Trim();
        settings.KitchenPantry.FreezerInventoryNotes = settings.KitchenPantry.FreezerInventoryNotes.Trim();
        settings.KitchenPantry.PantryStaplesNotes = settings.KitchenPantry.PantryStaplesNotes.Trim();
        settings.KitchenPantry.PreferredBrands = settings.KitchenPantry.PreferredBrands.Trim();
        settings.KitchenPantry.FreezerItems = (settings.KitchenPantry.FreezerItems ?? [])
            .Select(NormalizeFreezerInventoryItem)
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();

        settings.Shopping.PreferredUnits = PreferredUnitMode.All.Contains(settings.Shopping.PreferredUnits)
            ? settings.Shopping.PreferredUnits
            : PreferredUnitMode.RecipeUnits;
        if (!Enum.IsDefined(settings.Shopping.DefaultFreshShoppingDay))
        {
            settings.Shopping.DefaultFreshShoppingDay = DayOfWeek.Saturday;
        }

        if (!Enum.IsDefined(settings.Shopping.DefaultGeneralShoppingDay))
        {
            settings.Shopping.DefaultGeneralShoppingDay = DayOfWeek.Saturday;
        }
    }

    private static HouseholdMemberProfile NormalizeHouseholdMemberProfile(HouseholdMemberProfile member)
    {
        var normalized = CloneHouseholdMemberProfile(member);
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id)
            ? Guid.NewGuid().ToString("N")
            : normalized.Id.Trim();
        normalized.Name = normalized.Name.Trim();
        normalized.HeightCentimeters = Math.Clamp(normalized.HeightCentimeters, 0, 260);
        normalized.WeightKilograms = Math.Clamp(normalized.WeightKilograms, 0, 400);
        normalized.BodyFatPercentage = normalized.BodyFatPercentage is null
            ? null
            : Math.Clamp(normalized.BodyFatPercentage.Value, 0, 100);
        normalized.Sex = MemberSex.All.Contains(normalized.Sex) ? normalized.Sex : MemberSex.Unspecified;
        normalized.CurrentLifeStage = normalized.CurrentLifeStage.Trim();
        normalized.ActivityLevel = MemberActivityLevel.All.Contains(normalized.ActivityLevel)
            ? normalized.ActivityLevel
            : MemberActivityLevel.Moderate;
        normalized.WeeklyExercise = normalized.WeeklyExercise.Trim();
        normalized.Occupation = normalized.Occupation.Trim();
        normalized.HealthNotes = normalized.HealthNotes.Trim();
        normalized.FavouriteFoods = normalized.FavouriteFoods.Trim();
        normalized.FoodsToAvoid = normalized.FoodsToAvoid.Trim();
        normalized.FoodsToEncourage = normalized.FoodsToEncourage.Trim();
        normalized.SpiceTolerance = SpiceToleranceLevel.All.Contains(normalized.SpiceTolerance)
            ? normalized.SpiceTolerance
            : SpiceToleranceLevel.Medium;
        normalized.CookingPreferences = normalized.CookingPreferences.Trim();
        normalized.NutritionGoals = normalized.NutritionGoals
            .Where(goal => MemberNutritionGoal.All.Contains(goal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        normalized.CustomNutritionGoals = normalized.CustomNutritionGoals.Trim();
        normalized.MeasurementHistory = normalized.MeasurementHistory
            .Select(NormalizeBodyMeasurement)
            .Where(measurement => measurement.WeightKilograms > 0)
            .OrderByDescending(measurement => measurement.Date)
            .ToList();

        if (normalized.MeasurementHistory.Count == 0 && normalized.WeightKilograms > 0)
        {
            normalized.MeasurementHistory.Add(new BodyMeasurement
            {
                Date = normalized.MeasurementDate ?? DateTime.Today,
                WeightKilograms = normalized.WeightKilograms,
                HeightCentimeters = normalized.HeightCentimeters > 0 ? normalized.HeightCentimeters : null,
                BodyFatPercentage = normalized.BodyFatPercentage
            });
        }

        var latestMeasurement = normalized.MeasurementHistory
            .OrderByDescending(measurement => measurement.Date)
            .FirstOrDefault();
        if (latestMeasurement is not null)
        {
            normalized.MeasurementDate = latestMeasurement.Date;
            normalized.WeightKilograms = latestMeasurement.WeightKilograms;
            if (latestMeasurement.HeightCentimeters is > 0)
            {
                normalized.HeightCentimeters = latestMeasurement.HeightCentimeters.Value;
            }

            normalized.BodyFatPercentage = latestMeasurement.BodyFatPercentage;
        }

        return normalized;
    }

    private static void SyncCalculatedMacroGramTargets(HouseholdNutritionGoals goals)
    {
        goals.MinimumProteinGramsPerPerson = CalculateMacroGrams(goals.TargetCaloriesPerPerson, goals.ProteinPercent, 4);
        goals.TargetCarbohydrateGramsPerPerson = CalculateMacroGrams(goals.TargetCaloriesPerPerson, goals.CarbohydratePercent, 4);
        goals.TargetFatGramsPerPerson = CalculateMacroGrams(goals.TargetCaloriesPerPerson, goals.FatPercent, 9);
    }

    private static decimal CalculateMacroGrams(decimal calories, decimal percent, decimal caloriesPerGram) =>
        calories <= 0 || percent <= 0 || caloriesPerGram <= 0
            ? 0
            : Math.Round(calories * percent / 100m / caloriesPerGram, 1);

    private static BodyMeasurement NormalizeBodyMeasurement(BodyMeasurement measurement)
    {
        var normalized = CloneBodyMeasurement(measurement);
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id)
            ? Guid.NewGuid().ToString("N")
            : normalized.Id.Trim();
        normalized.Date = normalized.Date == default ? DateTime.Today : normalized.Date.Date;
        normalized.WeightKilograms = Math.Clamp(normalized.WeightKilograms, 0, 400);
        normalized.HeightCentimeters = normalized.HeightCentimeters is null
            ? null
            : Math.Clamp(normalized.HeightCentimeters.Value, 0, 260);
        normalized.BodyFatPercentage = normalized.BodyFatPercentage is null
            ? null
            : Math.Clamp(normalized.BodyFatPercentage.Value, 0, 100);
        normalized.Notes = normalized.Notes.Trim();
        return normalized;
    }

    private static FreezerInventoryItem NormalizeFreezerInventoryItem(FreezerInventoryItem item)
    {
        var normalized = CloneFreezerInventoryItem(item);
        normalized.Id = string.IsNullOrWhiteSpace(normalized.Id)
            ? Guid.NewGuid().ToString("N")
            : normalized.Id.Trim();
        normalized.Name = normalized.Name.Trim();
        normalized.Quantity = normalized.Quantity is null
            ? null
            : Math.Max(0, normalized.Quantity.Value);
        normalized.Unit = normalized.Unit.Trim();
        normalized.FrozenDate = normalized.FrozenDate?.Date;
        normalized.BestBeforeDate = normalized.BestBeforeDate?.Date;
        normalized.Notes = normalized.Notes.Trim();
        return normalized;
    }

    private static List<WeeklyFoodTarget> NormalizeWeeklyFoodTargets(WeeklyFoodRules rules)
    {
        var configured = (rules.Targets ?? [])
            .Select(target => new WeeklyFoodTarget
            {
                FoodGroup = NormalizeFoodGroup(target.FoodGroup),
                RuleType = WeeklyFoodRuleType.All.Contains(target.RuleType)
                    ? target.RuleType
                    : WeeklyFoodRuleType.Minimum,
                MealsPerWeek = Math.Clamp(target.MealsPerWeek, 0, 14)
            })
            .Where(target => !string.IsNullOrWhiteSpace(target.FoodGroup))
            .GroupBy(target => target.FoodGroup, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var target in DefaultWeeklyFoodTargets(rules))
        {
            configured.TryAdd(target.FoodGroup, target);
        }

        return configured.Values
            .OrderBy(target => WeeklyFoodTargetSortOrder(target.FoodGroup))
            .ToList();
    }

    private static IEnumerable<WeeklyFoodTarget> DefaultWeeklyFoodTargets(WeeklyFoodRules rules)
    {
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.BlueFish, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.WhiteFish, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 1 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Seafood, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 0 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Legumes, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = Math.Max(2, rules.MinimumLegumeMeals) };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Eggs, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Poultry, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 3 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.RedMeat, RuleType = WeeklyFoodRuleType.Maximum, MealsPerWeek = Math.Max(1, rules.MaximumRedMeatMeals) };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Vegetables, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 7 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Vegetarian, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Pasta, RuleType = WeeklyFoodRuleType.Maximum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Rice, RuleType = WeeklyFoodRuleType.Maximum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.HomemadeFastFood, RuleType = WeeklyFoodRuleType.Maximum, MealsPerWeek = 1 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.Desserts, RuleType = WeeklyFoodRuleType.Maximum, MealsPerWeek = 2 };
        yield return new WeeklyFoodTarget { FoodGroup = FoodGroupKind.VegetableRich, RuleType = WeeklyFoodRuleType.Minimum, MealsPerWeek = Math.Max(7, rules.MinimumVegetableRichMeals) };
    }

    private static void SyncLegacyWeeklyTargets(WeeklyFoodRules rules)
    {
        rules.MinimumFishMeals = rules.Targets
            .Where(target => target.FoodGroup is FoodGroupKind.BlueFish or FoodGroupKind.WhiteFish or FoodGroupKind.Fish)
            .Where(target => target.RuleType != WeeklyFoodRuleType.Maximum)
            .Sum(target => target.MealsPerWeek);
        rules.MinimumLegumeMeals = rules.Targets
            .FirstOrDefault(target => target.FoodGroup == FoodGroupKind.Legumes)?.MealsPerWeek
            ?? rules.MinimumLegumeMeals;
        rules.MaximumRedMeatMeals = rules.Targets
            .FirstOrDefault(target => target.FoodGroup == FoodGroupKind.RedMeat)?.MealsPerWeek
            ?? rules.MaximumRedMeatMeals;
        rules.MinimumVegetableRichMeals = rules.Targets
            .FirstOrDefault(target => target.FoodGroup == FoodGroupKind.VegetableRich)?.MealsPerWeek
            ?? rules.MinimumVegetableRichMeals;
    }

    private static int WeeklyFoodTargetSortOrder(string foodGroup)
    {
        var index = Array.IndexOf(WeeklyFoodTargetDisplayOrder, foodGroup);
        return index < 0 ? WeeklyFoodTargetDisplayOrder.Length : index;
    }

    private static readonly string[] WeeklyFoodTargetDisplayOrder =
    [
        FoodGroupKind.BlueFish,
        FoodGroupKind.WhiteFish,
        FoodGroupKind.Seafood,
        FoodGroupKind.Legumes,
        FoodGroupKind.Eggs,
        FoodGroupKind.Poultry,
        FoodGroupKind.RedMeat,
        FoodGroupKind.Vegetables,
        FoodGroupKind.Vegetarian,
        FoodGroupKind.Pasta,
        FoodGroupKind.Rice,
        FoodGroupKind.HomemadeFastFood,
        FoodGroupKind.Desserts,
        FoodGroupKind.VegetableRich
    ];

    private static int PlannerDaySortOrder(DayOfWeek day) =>
        day == DayOfWeek.Sunday ? 6 : (int)day - 1;

    private static string DefaultFoodGroupFor(DayOfWeek day) => day switch
    {
        DayOfWeek.Tuesday => FoodGroupKind.Fish,
        DayOfWeek.Wednesday => FoodGroupKind.Legumes,
        DayOfWeek.Thursday => FoodGroupKind.Fish,
        _ => FoodGroupKind.None
    };

    private static string NormalizeFoodGroup(string? foodGroup) =>
        FoodGroupKind.PlanningGroups.Contains(foodGroup)
            ? foodGroup!
            : FoodGroupKind.None;

    private static List<string> NormalizeFoodGroups(IEnumerable<string> foodGroups) =>
        foodGroups
            .Select(NormalizeFoodGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeNutritionState(string? nutritionState) =>
        NutritionRecordState.All.Contains(nutritionState ?? string.Empty)
            ? nutritionState!
            : NutritionRecordState.Unspecified;

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
        public string CatalanName { get; set; } = string.Empty;
        public string SpanishName { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = [];
        public string Category { get; set; } = string.Empty;
        public string Subcategory { get; set; } = string.Empty;
        public string DefaultUnit { get; set; } = string.Empty;
        public bool CanFreeze { get; set; }
        public string PantryCategory { get; set; } = string.Empty;
        public string NutritionState { get; set; } = string.Empty;
        public IngredientKnowledgeNutrition? Nutrition { get; set; }
        public string Source { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public DateTimeOffset? LastUpdated { get; set; }
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

    private sealed class HouseholdIngredientPreferenceSeedFile
    {
        public int SchemaVersion { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public List<HouseholdIngredientPreferenceSeedItem> Items { get; set; } = [];
        public List<string> UnresolvedItems { get; set; } = [];
    }

    private sealed class HouseholdIngredientPreferenceSeedItem
    {
        public List<string> MatchNames { get; set; } = [];
        public bool IsFrequentlyUsed { get; set; } = true;
        public bool IsUsuallyAvailable { get; set; } = true;
        public string UseFrequency { get; set; } = IngredientUseFrequency.Frequent;
        public string PreferredAlias { get; set; } = string.Empty;
        public string HouseholdNotes { get; set; } = string.Empty;
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
    public HouseholdPlanningSettings PlanningSettings { get; set; } = new();
    public List<HouseholdIngredientPreference> HouseholdIngredientPreferences { get; set; } = [];
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
    public int LinkedIngredients { get; set; }
    public int UnresolvedIngredients { get; set; }
    public int Products { get; set; }
    public int MealPlanSlots { get; set; }
    public int PlannedRecipes { get; set; }
    public int ShoppingLists { get; set; }
    public int ShoppingItems { get; set; }
    public int PantryItems { get; set; }
    public int FreezerItems { get; set; }
    public int HouseholdIngredientPreferences { get; set; }
    public int HouseholdMembers { get; set; }
    public int Measurements { get; set; }
    public int RecipeIdeas { get; set; }
    public int ProductBacklogItems { get; set; }

    public static DataBackupSummary From(LocalAppState state) => new()
    {
        Recipes = state.Recipes.Count,
        DraftRecipes = state.Recipes.Count(recipe => recipe.IsDraft),
        Ingredients = state.Ingredients.Count,
        LinkedIngredients = state.Recipes.Sum(recipe => recipe.Ingredients.Count(ingredient =>
            !string.IsNullOrWhiteSpace(ingredient.Ingredient?.KnowledgeId))),
        UnresolvedIngredients = state.Recipes.Sum(recipe => recipe.Ingredients.Count(ingredient =>
            string.IsNullOrWhiteSpace(ingredient.Ingredient?.KnowledgeId))),
        Products = state.Products.Count,
        MealPlanSlots = state.MealPlanSlots.Count,
        PlannedRecipes = state.MealPlanSlots.Sum(slot => slot.PlannedRecipes.Count),
        ShoppingLists = state.ShoppingLists.Count,
        ShoppingItems = state.ShoppingLists.Sum(list => list.Items.Count),
        PantryItems = state.PantryItems.Count,
        FreezerItems = state.PlanningSettings?.KitchenPantry?.FreezerItems?.Count ?? 0,
        HouseholdIngredientPreferences = state.HouseholdIngredientPreferences.Count,
        HouseholdMembers = state.PlanningSettings?.Members?.Count ?? 0,
        Measurements = state.PlanningSettings?.Members?.Sum(member => member.MeasurementHistory?.Count ?? 0) ?? 0,
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
