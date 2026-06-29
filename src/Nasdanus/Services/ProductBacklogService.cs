using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class ProductBacklogService(
    BrowserAppStore store,
    NavigationManager navigation,
    IJSRuntime jsRuntime)
{
    public async Task<List<ProductBacklogItem>> GetAllAsync()
    {
        var state = await store.GetStateAsync();
        return state.ProductBacklogItems
            .OrderByDescending(item => item.CreatedAt)
            .Select(Clone)
            .ToList();
    }

    public async Task<ProductBacklogItem?> GetByIdAsync(int id)
    {
        var state = await store.GetStateAsync();
        var item = state.ProductBacklogItems.FirstOrDefault(item => item.Id == id);
        return item is null ? null : Clone(item);
    }

    public async Task<ProductBacklogContext> CaptureContextAsync(ProductBacklogItem? existingItem = null)
    {
        var context = existingItem?.Context is not null
            ? Clone(existingItem.Context)
            : new ProductBacklogContext();

        if (existingItem is not null)
        {
            context.FeedbackId = existingItem.Id;
            return context;
        }

        context.CurrentUrl = navigation.Uri;
        context.CapturedAt = DateTime.UtcNow;
        context.BrowserInformation = await BrowserInfoAsync();

        var relativePath = navigation.ToBaseRelativePath(navigation.Uri);
        var path = relativePath.Split('?', '#')[0].Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        context.Page = PageNameFor(segments);
        var state = await store.GetStateAsync();

        if (TryGetRecipeId(segments, out var recipeId))
        {
            var recipe = store.FindRecipe(state, recipeId);
            context.RecipeId = recipeId;
            context.RecipeName = recipe?.Name ?? string.Empty;
        }

        var query = QueryParameters(navigation.Uri);
        if (query.TryGetValue("plannedMealId", out var plannedIdText)
            && int.TryParse(plannedIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plannedRecipeId))
        {
            var plannedContext = state.MealPlanSlots
                .SelectMany(slot => slot.PlannedRecipes.Select(plannedRecipe => new { Slot = slot, PlannedRecipe = plannedRecipe }))
                .FirstOrDefault(match => match.PlannedRecipe.Id == plannedRecipeId);
            if (plannedContext is not null)
            {
                context.PlannerWeek = PlannerService.WeekStart(plannedContext.Slot.Date);
                context.PlannerDay = plannedContext.Slot.Date;
                context.Meal = plannedContext.Slot.MealKind == MealKind.Lunch ? "Lunch" : "Dinner";
            }
        }

        if (string.Equals(context.Page, ProductBacklogScope.Planner, StringComparison.OrdinalIgnoreCase))
        {
            context.PlannerWeek = PlannerService.WeekStart(DateOnly.FromDateTime(DateTime.Today));
        }

        if (string.Equals(context.Page, ProductBacklogScope.ShoppingList, StringComparison.OrdinalIgnoreCase))
        {
            context.ShoppingWeek = PlannerService.WeekStart(DateOnly.FromDateTime(DateTime.Today));
        }

        return context;
    }

    public async Task<ProductBacklogItem> CreateAsync(ProductBacklogEditRequest request, ProductBacklogContext context)
    {
        var state = await store.GetStateAsync();
        var item = new ProductBacklogItem
        {
            Id = store.NextId(state),
            Type = NormalizeType(request.Type),
            Scope = NormalizeScope(request.Scope),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = NormalizePriority(request.Priority),
            Status = NormalizeStatus(request.Status),
            DuplicateOfId = request.DuplicateOfId,
            Labels = NormalizeLabels(request.Labels),
            TargetVersion = request.TargetVersion.Trim(),
            Decision = NormalizeDecision(request.Decision),
            ResolutionNotes = request.ResolutionNotes.Trim(),
            ApplicationVersion = AppInfo.Version,
            Context = Clone(context),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (ProductBacklogStatus.IsClosed(item.Status))
        {
            item.ClosedAt = DateTime.UtcNow;
        }

        item.Context.FeedbackId = item.Id;
        state.ProductBacklogItems.Add(item);
        await store.SaveAsync();
        return Clone(item);
    }

    public async Task UpdateAsync(int id, ProductBacklogEditRequest request)
    {
        var state = await store.GetStateAsync();
        var item = state.ProductBacklogItems.FirstOrDefault(item => item.Id == id);
        if (item is null)
        {
            return;
        }

        item.Type = NormalizeType(request.Type);
        item.Scope = NormalizeScope(request.Scope);
        item.Title = request.Title.Trim();
        item.Description = request.Description.Trim();
        item.Priority = NormalizePriority(request.Priority);
        item.Status = NormalizeStatus(request.Status);
        item.DuplicateOfId = request.DuplicateOfId;
        item.Labels = NormalizeLabels(request.Labels);
        item.TargetVersion = request.TargetVersion.Trim();
        item.Decision = NormalizeDecision(request.Decision);
        item.ResolutionNotes = request.ResolutionNotes.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        item.ClosedAt = ProductBacklogStatus.IsClosed(item.Status)
            ? item.ClosedAt ?? DateTime.UtcNow
            : null;

        await store.SaveAsync();
    }

    public async Task MarkCompletedAsync(int id)
    {
        var state = await store.GetStateAsync();
        var item = state.ProductBacklogItems.FirstOrDefault(item => item.Id == id);
        if (item is null)
        {
            return;
        }

        item.Status = ProductBacklogStatus.Completed;
        item.Decision = string.IsNullOrWhiteSpace(item.Decision)
            ? ProductBacklogDecision.Implemented
            : item.Decision;
        item.ClosedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await store.SaveAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var state = await store.GetStateAsync();
        var item = state.ProductBacklogItems.FirstOrDefault(item => item.Id == id);
        if (item is null)
        {
            return;
        }

        state.ProductBacklogItems.Remove(item);
        await store.SaveAsync();
    }

    public async Task CopyToClipboardAsync(string text)
    {
        await jsRuntime.InvokeVoidAsync("nasdanusData.copyText", text);
    }

    public static ProductBacklogEditRequest ToRequest(ProductBacklogItem item) => new()
    {
        Type = item.Type,
        Scope = item.Scope,
        Title = item.Title,
        Description = item.Description,
        Priority = item.Priority,
        Status = item.Status,
        DuplicateOfId = item.DuplicateOfId,
        Labels = [.. item.Labels],
        TargetVersion = item.TargetVersion,
        Decision = item.Decision,
        ResolutionNotes = item.ResolutionNotes
    };

    public static string FormatDiagnostic(ProductBacklogItem item) => FormatDiagnostic(
        item.Context,
        item.ApplicationVersion,
        item.Title,
        item.Id);

    public static string FormatDiagnostic(
        ProductBacklogContext context,
        string applicationVersion = AppInfo.Version,
        string title = "",
        int? itemId = null)
    {
        var lines = new List<string>
        {
            $"Application Version: {applicationVersion}",
            $"Page: {ValueOrDash(context.Page)}",
            $"URL: {ValueOrDash(RelativeUrl(context.CurrentUrl))}",
            $"Date: {context.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"
        };

        if (itemId is not null)
        {
            lines.Add($"Feedback Id: {itemId}");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            lines.Add($"Title: {title}");
        }

        if (context.RecipeId is not null || !string.IsNullOrWhiteSpace(context.RecipeName))
        {
            lines.Add($"Recipe: {ValueOrDash(context.RecipeName)}{(context.RecipeId is null ? string.Empty : $" (Id {context.RecipeId})")}");
        }

        if (context.CookingStepNumber is not null)
        {
            lines.Add($"Step: {context.CookingStepNumber}");
        }

        if (context.PlannerWeek is not null)
        {
            lines.Add($"Planner Week: {context.PlannerWeek:yyyy-MM-dd}");
        }

        if (context.PlannerDay is not null)
        {
            lines.Add($"Planner Day: {context.PlannerDay:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(context.Meal))
        {
            lines.Add($"Meal: {context.Meal}");
        }

        if (context.ShoppingWeek is not null)
        {
            lines.Add($"Shopping Week: {context.ShoppingWeek:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(context.ShoppingCategory))
        {
            lines.Add($"Shopping Category: {context.ShoppingCategory}");
        }

        if (!string.IsNullOrWhiteSpace(context.PantryItemName))
        {
            lines.Add($"Pantry Item: {context.PantryItemName}");
        }

        if (!string.IsNullOrWhiteSpace(context.BrowserInformation))
        {
            lines.Add($"Browser: {context.BrowserInformation}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatMarkdown(ProductBacklogItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {item.Title}");
        builder.AppendLine();
        builder.AppendLine($"- Type: {ProductBacklogType.ToDisplayName(item.Type)}");
        builder.AppendLine($"- Scope: {item.Scope}");
        builder.AppendLine($"- Priority: {item.Priority}");
        builder.AppendLine($"- Version: {item.ApplicationVersion}");
        builder.AppendLine($"- Target Version: {ValueOrDash(item.TargetVersion)}");
        builder.AppendLine($"- Status: {item.Status}");
        if (!string.IsNullOrWhiteSpace(item.Decision))
        {
            builder.AppendLine($"- Decision: {item.Decision}");
        }

        builder.AppendLine();
        builder.AppendLine("## Description");
        builder.AppendLine(string.IsNullOrWhiteSpace(item.Description) ? "_No description._" : item.Description);
        builder.AppendLine();
        builder.AppendLine("## Context");
        builder.AppendLine("```text");
        builder.AppendLine(FormatDiagnostic(item));
        builder.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(item.ResolutionNotes))
        {
            builder.AppendLine();
            builder.AppendLine("## Resolution Notes");
            builder.AppendLine(item.ResolutionNotes);
        }

        return builder.ToString();
    }

    private async Task<string> BrowserInfoAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>("nasdanusData.getBrowserInfo");
        }
        catch (JSException)
        {
            return string.Empty;
        }
    }

    private static ProductBacklogItem Clone(ProductBacklogItem item) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Scope = item.Scope,
        Title = item.Title,
        Description = item.Description,
        Priority = item.Priority,
        Status = item.Status,
        DuplicateOfId = item.DuplicateOfId,
        Labels = [.. item.Labels],
        ApplicationVersion = item.ApplicationVersion,
        TargetVersion = item.TargetVersion,
        Decision = item.Decision,
        ResolutionNotes = item.ResolutionNotes,
        Context = Clone(item.Context),
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        ClosedAt = item.ClosedAt
    };

    private static ProductBacklogContext Clone(ProductBacklogContext context) => new()
    {
        FeedbackId = context.FeedbackId,
        Page = context.Page,
        CurrentUrl = context.CurrentUrl,
        CapturedAt = context.CapturedAt,
        BrowserInformation = context.BrowserInformation,
        RecipeId = context.RecipeId,
        RecipeName = context.RecipeName,
        PlannerWeek = context.PlannerWeek,
        PlannerDay = context.PlannerDay,
        Meal = context.Meal,
        CookingStepNumber = context.CookingStepNumber,
        ShoppingWeek = context.ShoppingWeek,
        ShoppingCategory = context.ShoppingCategory,
        PantryItemId = context.PantryItemId,
        PantryItemName = context.PantryItemName
    };

    private static bool TryGetRecipeId(string[] segments, out int recipeId)
    {
        recipeId = 0;
        if (segments.Length >= 2
            && string.Equals(segments[0], "recipes", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out recipeId))
        {
            return true;
        }

        if (segments.Length >= 2
            && string.Equals(segments[0], "cook", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out recipeId))
        {
            return true;
        }

        return false;
    }

    private static string PageNameFor(string[] segments)
    {
        if (segments.Length == 0)
        {
            return ProductBacklogScope.Home;
        }

        return segments[0].ToLowerInvariant() switch
        {
            "planner" => ProductBacklogScope.Planner,
            "recipes" when segments.Length == 1 => ProductBacklogScope.RecipeList,
            "recipes" when segments.Length >= 3 && string.Equals(segments[2], "edit", StringComparison.OrdinalIgnoreCase) => ProductBacklogScope.RecipeEditor,
            "recipes" => ProductBacklogScope.RecipeDetails,
            "cook" => ProductBacklogScope.CookingMode,
            "shopping" => ProductBacklogScope.ShoppingList,
            "pantry" => ProductBacklogScope.Pantry,
            "data" => ProductBacklogScope.Settings,
            "backlog" => ProductBacklogScope.General,
            _ => ProductBacklogScope.General
        };
    }

    private static Dictionary<string, string> QueryParameters(string absoluteUri)
    {
        var uri = new Uri(absoluteUri);
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return [];
        }

        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1]),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeType(string type) =>
        ProductBacklogType.All.Contains(type) ? type : ProductBacklogType.Idea;

    private static string NormalizeScope(string scope) =>
        ProductBacklogScope.All.Contains(scope) ? scope : ProductBacklogScope.General;

    private static string NormalizePriority(string priority) =>
        ProductBacklogPriority.All.Contains(priority) ? priority : ProductBacklogPriority.Medium;

    private static string NormalizeStatus(string status) =>
        ProductBacklogStatus.All.Contains(status) ? status : ProductBacklogStatus.New;

    private static string NormalizeDecision(string decision) =>
        string.IsNullOrWhiteSpace(decision)
            ? string.Empty
            : ProductBacklogDecision.All.Contains(decision)
                ? decision
                : string.Empty;

    private static List<string> NormalizeLabels(IEnumerable<string> labels) =>
        labels
            .Select(label => label.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label)
            .ToList();

    private static string RelativeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return string.Join(string.Empty, uri.PathAndQuery, uri.Fragment);
    }

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;
}
