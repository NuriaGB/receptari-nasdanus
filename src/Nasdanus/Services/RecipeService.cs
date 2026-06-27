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
            .Distinct()
            .OrderBy(category => category)
            .AsNoTracking()
            .ToListAsync();
    }
}
