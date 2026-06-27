using Microsoft.EntityFrameworkCore;
using Nasdanus.Data;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class RecipeService(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task<List<Recipe>> GetAllAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(recipe => recipe.Ingredients.OrderBy(ingredient => ingredient.Order))
            .Include(recipe => recipe.Steps.OrderBy(step => step.Order))
            .OrderBy(recipe => recipe.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Recipe?> GetByIdAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Recipes
            .Include(recipe => recipe.Ingredients.OrderBy(ingredient => ingredient.Order))
            .Include(recipe => recipe.Steps.OrderBy(step => step.Order))
            .AsNoTracking()
            .FirstOrDefaultAsync(recipe => recipe.Id == id);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Recipes
            .Select(recipe => recipe.Category)
            .Where(category => category != string.Empty)
            .Distinct()
            .OrderBy(category => category)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Recipe> CreateQuickDraftAsync(QuickAddRecipeRequest request)
    {
        var ingredients = ParseLines(request.IngredientsText)
            .Select((line, index) => new RecipeIngredient
            {
                Name = line,
                Order = index + 1
            })
            .ToList();

        var steps = ParseLines(request.StepsText)
            .Select((line, index) => new RecipeStep
            {
                Title = $"Pas {index + 1}",
                Instruction = line,
                Order = index + 1
            })
            .ToList();

        var recipe = new Recipe
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            PreparationTimeMinutes = request.PreparationTimeMinutes ?? 0,
            CookingTimeMinutes = request.CookingTimeMinutes ?? 0,
            Difficulty = 1,
            Servings = request.Servings ?? 0,
            Ingredients = ingredients,
            Steps = steps,
            Status = IsIncomplete(request, ingredients.Count, steps.Count)
                ? RecipeStatus.Draft
                : RecipeStatus.Active
        };

        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe;
    }

    private static bool IsIncomplete(QuickAddRecipeRequest request, int ingredientCount, int stepCount) =>
        string.IsNullOrWhiteSpace(request.Category)
        || ingredientCount == 0
        || stepCount == 0
        || request.PreparationTimeMinutes is null
        || request.CookingTimeMinutes is null
        || request.Servings is null;

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
