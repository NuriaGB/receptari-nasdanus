using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class BrowserAppStore(HttpClient httpClient, IJSRuntime jsRuntime)
{
    private const string StorageKey = "nasdanus.static.state.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false
    };

    private LocalAppState? state;

    public async Task<LocalAppState> GetStateAsync()
    {
        if (state is not null)
        {
            return state;
        }

        var storedState = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        state = string.IsNullOrWhiteSpace(storedState)
            ? await LoadSeedAsync()
            : JsonSerializer.Deserialize<LocalAppState>(storedState, JsonOptions) ?? await LoadSeedAsync();

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
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
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
    public List<ShoppingList> ShoppingLists { get; set; } = [];
}
