using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed record RecipeLibraryProfile(
    string DocumentationStatus,
    string CookingStatus,
    int CompletionPercent,
    IReadOnlyList<RecipeCompletionItem> CompletionItems,
    RecipeWorkflowAction NextAction,
    int TotalIngredientCount,
    int LinkedIngredientCount,
    int NutritionRelevantIngredientCount,
    int NutritionLinkedIngredientCount,
    bool HasUnresolvedIngredients,
    bool HasMissingNutrition,
    bool IsHighProtein,
    bool IsQuickMeal,
    bool IsSeasonal,
    bool IsFreezerFriendly,
    bool IsBatchCooking,
    RecipeFoodProfile FoodProfile);

public sealed record RecipeCompletionItem(string Label, bool IsComplete);

public sealed record RecipeWorkflowAction(
    string Key,
    string Label,
    string FilterLabel,
    string DashboardLabel,
    string TargetKind,
    string TargetFragment,
    bool IsComplete = false);

public sealed record RecipeCurrentWorkItem(RecipeWorkflowAction Action, int Count);

public sealed record RecipeLibraryDashboard(
    int TotalRecipes,
    int EmptyRecipes,
    int DraftRecipes,
    int CompleteRecipes,
    int NeverCookedRecipes,
    int TestedRecipes,
    int FamilyApprovedRecipes,
    int FavouriteRecipes,
    int FullyLinkedRecipes,
    int PartiallyLinkedRecipes,
    int UnresolvedRecipes,
    IReadOnlyList<RecipeCurrentWorkItem> CurrentWork);

public static class RecipeWorkflowActionKind
{
    public const string AddIngredients = "AddIngredients";
    public const string LinkIngredients = "LinkIngredients";
    public const string AddNutritionLinks = "AddNutritionLinks";
    public const string AddCookingSteps = "AddCookingSteps";
    public const string AddPreparationTime = "AddPreparationTime";
    public const string AddCookingTime = "AddCookingTime";
    public const string AddServings = "AddServings";
    public const string AddCategories = "AddCategories";
    public const string AddTags = "AddTags";
    public const string AddEquipment = "AddEquipment";
    public const string AddNotes = "AddNotes";
    public const string AddImage = "AddImage";
    public const string TestRecipe = "TestRecipe";
    public const string ReviewAfterCooking = "ReviewAfterCooking";
    public const string Done = "Done";
}

public static class RecipeWorkflowTargetKind
{
    public const string Edit = "Edit";
    public const string Cook = "Cook";
    public const string Details = "Details";
}

public static class RecipeDocumentationStatus
{
    public const string Empty = "Empty";
    public const string Draft = "Draft";
    public const string Complete = "Complete";

    public static readonly string[] All = [Empty, Draft, Complete];

    public static string ToDisplayName(string status) => status switch
    {
        Empty => "Empty",
        Draft => "Draft",
        Complete => "Complete",
        _ => status
    };
}

public static class RecipeCookingStatus
{
    public const string NeverCooked = "NeverCooked";
    public const string Tested = "Tested";
    public const string FamilyApproved = "FamilyApproved";
    public const string Favourite = "Favourite";

    public static readonly string[] All = [NeverCooked, Tested, FamilyApproved, Favourite];

    public static string ToDisplayName(string status) => status switch
    {
        NeverCooked => "Never Cooked",
        Tested => "Tested",
        FamilyApproved => "Family Approved",
        Favourite => "Favourite",
        _ => status
    };
}

public static class RecipeLibraryFilter
{
    public const string Favourite = "Favourite";
    public const string Fish = "Fish";
    public const string Chicken = "Chicken";
    public const string Meat = "Meat";
    public const string Pasta = "Pasta";
    public const string Salad = "Salad";
    public const string Legumes = "Legumes";
    public const string Soup = "Soup";
    public const string Dessert = "Dessert";
    public const string HighProtein = "HighProtein";
    public const string QuickMeals = "QuickMeals";
    public const string Seasonal = "Seasonal";
    public const string FreezerFriendly = "FreezerFriendly";
    public const string BatchCooking = "BatchCooking";
    public const string Vegetarian = "Vegetarian";
    public const string WithoutNutrition = "WithoutNutrition";
    public const string UnresolvedIngredients = "UnresolvedIngredients";
}

public static class RecipeLibraryAnalysis
{
    public static readonly RecipeWorkflowAction[] WorkflowActions =
    [
        new(RecipeWorkflowActionKind.AddIngredients, "Add ingredients", "Need ingredients", "need ingredients", RecipeWorkflowTargetKind.Edit, "ingredients"),
        new(RecipeWorkflowActionKind.LinkIngredients, "Link ingredients", "Need ingredient links", "need ingredient linking", RecipeWorkflowTargetKind.Edit, "ingredients"),
        new(RecipeWorkflowActionKind.AddNutritionLinks, "Add nutrition links", "Need nutrition links", "need nutrition links", RecipeWorkflowTargetKind.Edit, "ingredients"),
        new(RecipeWorkflowActionKind.AddCookingSteps, "Add cooking steps", "Need cooking steps", "need cooking steps", RecipeWorkflowTargetKind.Edit, "steps"),
        new(RecipeWorkflowActionKind.AddPreparationTime, "Add preparation time", "Need preparation time", "need preparation time", RecipeWorkflowTargetKind.Edit, "basics"),
        new(RecipeWorkflowActionKind.AddCookingTime, "Add cooking time", "Need cooking time", "need cooking time", RecipeWorkflowTargetKind.Edit, "basics"),
        new(RecipeWorkflowActionKind.AddServings, "Add servings", "Need servings", "need servings", RecipeWorkflowTargetKind.Edit, "basics"),
        new(RecipeWorkflowActionKind.AddCategories, "Add categories", "Need categories", "need categories", RecipeWorkflowTargetKind.Edit, "basics"),
        new(RecipeWorkflowActionKind.AddTags, "Add tags", "Need tags", "need tags", RecipeWorkflowTargetKind.Edit, "tags"),
        new(RecipeWorkflowActionKind.AddEquipment, "Add equipment", "Need equipment", "need equipment", RecipeWorkflowTargetKind.Edit, "tags"),
        new(RecipeWorkflowActionKind.AddNotes, "Add notes", "Need notes", "need notes", RecipeWorkflowTargetKind.Edit, "notes"),
        new(RecipeWorkflowActionKind.AddImage, "Add image", "Need images", "only need a photo", RecipeWorkflowTargetKind.Edit, "image"),
        new(RecipeWorkflowActionKind.TestRecipe, "Test recipe", "Need testing", "need testing", RecipeWorkflowTargetKind.Cook, string.Empty),
        new(RecipeWorkflowActionKind.ReviewAfterCooking, "Review after cooking", "Need review", "are ready for family approval", RecipeWorkflowTargetKind.Details, "recipe-status"),
        new(RecipeWorkflowActionKind.Done, "Family approved", "Family approved", "are family approved", RecipeWorkflowTargetKind.Details, "recipe-status", IsComplete: true)
    ];

    public static RecipeLibraryProfile Analyze(Recipe recipe)
    {
        var foodProfile = RecipeFoodClassifier.Classify(recipe);
        var totalIngredients = recipe.Ingredients.Count;
        var linkedIngredients = recipe.Ingredients.Count(ingredient =>
            !string.IsNullOrWhiteSpace(ingredient.Ingredient?.KnowledgeId));
        var nutritionRelevantIngredients = recipe.Ingredients.Count(ingredient =>
            !IngredientNutritionRules.ShouldIgnoreForNutrition(ingredient.Ingredient));
        var nutritionLinkedIngredients = recipe.Ingredients.Count(ingredient =>
            !IngredientNutritionRules.ShouldIgnoreForNutrition(ingredient.Ingredient)
            && IngredientNutritionRules.HasUsableNutritionForCalculation(ingredient.Ingredient));
        var hasUnresolvedIngredients = totalIngredients == 0 || linkedIngredients < totalIngredients;
        var hasMissingNutrition = totalIngredients == 0 || nutritionLinkedIngredients < nutritionRelevantIngredients;
        var completionItems = CompletionItems(recipe, totalIngredients, linkedIngredients, nutritionLinkedIngredients, nutritionRelevantIngredients);
        var nextAction = NextAction(recipe, totalIngredients, linkedIngredients, nutritionLinkedIngredients, nutritionRelevantIngredients);
        var completionPercent = (int)Math.Round(
            completionItems.Count(item => item.IsComplete) * 100m / completionItems.Count,
            MidpointRounding.AwayFromZero);

        return new RecipeLibraryProfile(
            DocumentationStatus: DocumentationStatus(recipe),
            CookingStatus: CookingStatus(recipe),
            CompletionPercent: completionPercent,
            CompletionItems: completionItems,
            NextAction: nextAction,
            TotalIngredientCount: totalIngredients,
            LinkedIngredientCount: linkedIngredients,
            NutritionRelevantIngredientCount: nutritionRelevantIngredients,
            NutritionLinkedIngredientCount: nutritionLinkedIngredients,
            HasUnresolvedIngredients: hasUnresolvedIngredients,
            HasMissingNutrition: hasMissingNutrition,
            IsHighProtein: IsHighProtein(recipe),
            IsQuickMeal: TotalMinutes(recipe) <= 30,
            IsSeasonal: HasRecipeSignal(recipe, "seasonal", "temporada", "estiu", "hivern", "primavera", "tardor"),
            IsFreezerFriendly: HasRecipeSignal(recipe, "freezer", "freeze", "congel", "congelador"),
            IsBatchCooking: HasRecipeSignal(recipe, "batch", "meal prep", "lot", "batch cooking", "sobres"),
            FoodProfile: foodProfile);
    }

    public static RecipeLibraryDashboard Dashboard(IEnumerable<Recipe> recipes)
    {
        var profiles = recipes
            .Select(Analyze)
            .ToList();
        var currentWork = WorkflowActions
            .Where(action => !action.IsComplete)
            .Select(action => new RecipeCurrentWorkItem(
                action,
                profiles.Count(profile => profile.NextAction.Key == action.Key)))
            .Where(item => item.Count > 0)
            .ToList();

        return new RecipeLibraryDashboard(
            TotalRecipes: profiles.Count,
            EmptyRecipes: profiles.Count(profile => profile.DocumentationStatus == RecipeDocumentationStatus.Empty),
            DraftRecipes: profiles.Count(profile => profile.DocumentationStatus == RecipeDocumentationStatus.Draft),
            CompleteRecipes: profiles.Count(profile => profile.DocumentationStatus == RecipeDocumentationStatus.Complete),
            NeverCookedRecipes: profiles.Count(profile => profile.CookingStatus == RecipeCookingStatus.NeverCooked),
            TestedRecipes: profiles.Count(profile => profile.CookingStatus == RecipeCookingStatus.Tested),
            FamilyApprovedRecipes: profiles.Count(profile => profile.CookingStatus == RecipeCookingStatus.FamilyApproved),
            FavouriteRecipes: profiles.Count(profile => profile.CookingStatus == RecipeCookingStatus.Favourite),
            FullyLinkedRecipes: profiles.Count(profile => profile.TotalIngredientCount > 0 && profile.LinkedIngredientCount == profile.TotalIngredientCount),
            PartiallyLinkedRecipes: profiles.Count(profile => profile.LinkedIngredientCount > 0 && profile.LinkedIngredientCount < profile.TotalIngredientCount),
            UnresolvedRecipes: profiles.Count(profile => profile.TotalIngredientCount == 0 || profile.LinkedIngredientCount == 0),
            CurrentWork: currentWork);
    }

    public static bool MatchesSearch(Recipe recipe, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var normalized = search.Trim();
        return recipe.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || recipe.Category.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || recipe.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || recipe.Tags.Any(tag => tag.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            || recipe.Ingredients.Any(ingredient => ingredient.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesFilter(Recipe recipe, RecipeLibraryProfile profile, string filter) => filter switch
    {
        RecipeLibraryFilter.Favourite => profile.CookingStatus == RecipeCookingStatus.Favourite,
        RecipeLibraryFilter.Fish => profile.FoodProfile.IsFish,
        RecipeLibraryFilter.Chicken => profile.FoodProfile.IsChicken,
        RecipeLibraryFilter.Meat => profile.FoodProfile.IsMeat,
        RecipeLibraryFilter.Pasta => profile.FoodProfile.IsPasta,
        RecipeLibraryFilter.Salad => ContainsRecipeText(recipe, "amanida", "salad"),
        RecipeLibraryFilter.Legumes => profile.FoodProfile.IsLegume,
        RecipeLibraryFilter.Soup => ContainsRecipeText(recipe, "sopa", "soup", "crema", "brou"),
        RecipeLibraryFilter.Dessert => profile.FoodProfile.IsDessert,
        RecipeLibraryFilter.HighProtein => profile.IsHighProtein,
        RecipeLibraryFilter.QuickMeals => profile.IsQuickMeal,
        RecipeLibraryFilter.Seasonal => profile.IsSeasonal,
        RecipeLibraryFilter.FreezerFriendly => profile.IsFreezerFriendly,
        RecipeLibraryFilter.BatchCooking => profile.IsBatchCooking,
        RecipeLibraryFilter.Vegetarian => profile.FoodProfile.IsVegetarian,
        RecipeLibraryFilter.WithoutNutrition => profile.HasMissingNutrition,
        RecipeLibraryFilter.UnresolvedIngredients => profile.HasUnresolvedIngredients,
        _ => true
    };

    public static bool MatchesCookingStatus(RecipeLibraryProfile profile, string status) => status switch
    {
        "" => true,
        RecipeCookingStatus.Tested => profile.CookingStatus is RecipeCookingStatus.Tested
            or RecipeCookingStatus.FamilyApproved
            or RecipeCookingStatus.Favourite,
        RecipeCookingStatus.FamilyApproved => profile.CookingStatus is RecipeCookingStatus.FamilyApproved
            or RecipeCookingStatus.Favourite,
        _ => profile.CookingStatus == status
    };

    public static bool IsUsableForPlanning(Recipe recipe)
    {
        var profile = Analyze(recipe);
        return profile.DocumentationStatus == RecipeDocumentationStatus.Complete;
    }

    public static string WorkHref(Recipe recipe, RecipeWorkflowAction action) => action.TargetKind switch
    {
        RecipeWorkflowTargetKind.Cook => recipe.Servings > 0
            ? $"cook/{recipe.Id}?servings={recipe.Servings}"
            : $"cook/{recipe.Id}",
        RecipeWorkflowTargetKind.Edit => string.IsNullOrWhiteSpace(action.TargetFragment)
            ? $"recipes/{recipe.Id}/edit"
            : $"recipes/{recipe.Id}/edit#{action.TargetFragment}",
        _ => string.IsNullOrWhiteSpace(action.TargetFragment)
            ? $"recipes/{recipe.Id}"
            : $"recipes/{recipe.Id}#{action.TargetFragment}"
    };

    public static RecipeWorkflowAction? WorkflowActionFor(string key) =>
        WorkflowActions.FirstOrDefault(action => string.Equals(action.Key, key, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<RecipeCompletionItem> CompletionItems(
        Recipe recipe,
        int totalIngredients,
        int linkedIngredients,
        int nutritionLinkedIngredients,
        int nutritionRelevantIngredients) =>
    [
        new("Ingredients", totalIngredients > 0),
        new("Ingredient links", totalIngredients > 0 && linkedIngredients == totalIngredients),
        new("Nutrition links", totalIngredients > 0 && nutritionLinkedIngredients >= nutritionRelevantIngredients),
        new("Steps", HasCookingSteps(recipe)),
        new("Preparation time", recipe.PreparationTimeMinutes > 0),
        new("Cooking time", recipe.CookingTimeMinutes > 0),
        new("Servings", recipe.Servings > 0),
        new("Categories", !string.IsNullOrWhiteSpace(recipe.Category)),
        new("Tags", recipe.Tags.Count > 0),
        new("Equipment", HasEquipment(recipe)),
        new("Notes", recipe.Notes.Count > 0),
        new("Photo", HasImage(recipe)),
        new("Tested", recipe.CookingHistory.Count > 0),
        new("Family approved", CookingStatus(recipe) is RecipeCookingStatus.FamilyApproved or RecipeCookingStatus.Favourite)
    ];

    private static RecipeWorkflowAction NextAction(
        Recipe recipe,
        int totalIngredients,
        int linkedIngredients,
        int nutritionLinkedIngredients,
        int nutritionRelevantIngredients)
    {
        if (totalIngredients == 0)
        {
            return Action(RecipeWorkflowActionKind.AddIngredients);
        }

        if (linkedIngredients < totalIngredients)
        {
            return Action(RecipeWorkflowActionKind.LinkIngredients);
        }

        if (nutritionLinkedIngredients < nutritionRelevantIngredients)
        {
            return Action(RecipeWorkflowActionKind.AddNutritionLinks);
        }

        if (!HasCookingSteps(recipe))
        {
            return Action(RecipeWorkflowActionKind.AddCookingSteps);
        }

        if (recipe.PreparationTimeMinutes <= 0)
        {
            return Action(RecipeWorkflowActionKind.AddPreparationTime);
        }

        if (recipe.CookingTimeMinutes <= 0)
        {
            return Action(RecipeWorkflowActionKind.AddCookingTime);
        }

        if (recipe.Servings <= 0)
        {
            return Action(RecipeWorkflowActionKind.AddServings);
        }

        if (string.IsNullOrWhiteSpace(recipe.Category))
        {
            return Action(RecipeWorkflowActionKind.AddCategories);
        }

        if (recipe.Tags.Count == 0)
        {
            return Action(RecipeWorkflowActionKind.AddTags);
        }

        if (!HasEquipment(recipe))
        {
            return Action(RecipeWorkflowActionKind.AddEquipment);
        }

        if (recipe.Notes.Count == 0)
        {
            return Action(RecipeWorkflowActionKind.AddNotes);
        }

        if (!HasImage(recipe))
        {
            return Action(RecipeWorkflowActionKind.AddImage);
        }

        if (recipe.CookingHistory.Count == 0)
        {
            return Action(RecipeWorkflowActionKind.TestRecipe);
        }

        var cookingStatus = CookingStatus(recipe);
        if (cookingStatus == RecipeCookingStatus.Tested)
        {
            return Action(RecipeWorkflowActionKind.ReviewAfterCooking);
        }

        return Action(RecipeWorkflowActionKind.Done);
    }

    private static string DocumentationStatus(Recipe recipe)
    {
        if (IsEmpty(recipe))
        {
            return RecipeDocumentationStatus.Empty;
        }

        return HasRequiredDocumentation(recipe)
            ? RecipeDocumentationStatus.Complete
            : RecipeDocumentationStatus.Draft;
    }

    private static string CookingStatus(Recipe recipe)
    {
        if (recipe.IsFavourite)
        {
            return RecipeCookingStatus.Favourite;
        }

        var cookedCount = recipe.CookingHistory.Count;
        if (cookedCount == 0)
        {
            return RecipeCookingStatus.NeverCooked;
        }

        if (cookedCount >= 3 || recipe.Rating >= 4 || recipe.CookingHistory.Any(session => session.Rating >= 4))
        {
            return RecipeCookingStatus.FamilyApproved;
        }

        return RecipeCookingStatus.Tested;
    }

    private static bool IsEmpty(Recipe recipe) =>
        string.IsNullOrWhiteSpace(recipe.Description)
        && string.IsNullOrWhiteSpace(recipe.Category)
        && recipe.Ingredients.Count == 0
        && recipe.Steps.Count == 0
        && recipe.PreparationTimeMinutes <= 0
        && recipe.CookingTimeMinutes <= 0
        && recipe.Servings <= 0
        && string.IsNullOrWhiteSpace(recipe.ImageUrl)
        && recipe.Tags.Count == 0
        && recipe.Notes.Count == 0;

    private static bool HasRequiredDocumentation(Recipe recipe) =>
        !string.IsNullOrWhiteSpace(recipe.Category)
        && recipe.Ingredients.Count > 0
        && recipe.Steps.Count > 0
        && recipe.PreparationTimeMinutes + recipe.CookingTimeMinutes > 0
        && recipe.Servings > 0;

    private static bool IsHighProtein(Recipe recipe)
    {
        var servings = recipe.Servings > 0 ? recipe.Servings : 1;
        var perServing = NutritionService.PerServing(
            NutritionService.CalculateRecipe(recipe, servings),
            servings);
        return perServing.ProteinGrams >= 25m;
    }

    private static int TotalMinutes(Recipe recipe) =>
        Math.Max(0, recipe.PreparationTimeMinutes) + Math.Max(0, recipe.CookingTimeMinutes);

    private static bool HasCookingSteps(Recipe recipe) =>
        recipe.Steps.Any(step => !string.IsNullOrWhiteSpace(step.Title) || !string.IsNullOrWhiteSpace(step.Instruction));

    private static bool HasEquipment(Recipe recipe) =>
        recipe.Tags.Any(tag => RecipeTagConventions.IsEquipmentTag(tag.Name))
        || HasRecipeSignal(recipe, "forn", "oven", "air fryer", "thermomix", "bbq", "vapor", "vaporera", "olla", "pressure", "paella", "cassola", "planxa", "grill", "motlle", "batedora", "turmix", "microones", "safata", "wok");

    private static bool HasImage(Recipe recipe) =>
        !string.IsNullOrWhiteSpace(recipe.ImageUrl)
        || HasRecipeSignal(recipe, "image", "photo", "imatge", "foto");

    private static RecipeWorkflowAction Action(string key) =>
        WorkflowActionFor(key) ?? WorkflowActions[^1];

    private static bool HasRecipeSignal(Recipe recipe, params string[] fragments) =>
        ContainsRecipeText(recipe, fragments);

    private static bool ContainsRecipeText(Recipe recipe, params string[] fragments)
    {
        var text = string.Join(
            " ",
            new[] { recipe.Name, recipe.Category, recipe.Description, recipe.SeasonalRecommendation, recipe.ImageUrl }
                .Concat(recipe.Tags.Select(tag => tag.Name))
                .Concat(recipe.PlanningMetadata.Select(metadata => $"{metadata.Kind} {metadata.Value} {metadata.Notes}"))
                .Concat(recipe.Ingredients.Select(ingredient => ingredient.DisplayName)));

        return fragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
