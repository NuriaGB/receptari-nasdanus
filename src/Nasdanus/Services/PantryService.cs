using Microsoft.EntityFrameworkCore;
using Nasdanus.Data;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class PantryService(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task<List<PantryItem>> GetAllAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.PantryItems
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddAsync(PantryItemEditRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var normalized = IngredientNameNormalizer.Normalize(name);
        var existingNames = await db.PantryItems
            .AsNoTracking()
            .Select(item => item.Name)
            .ToListAsync();
        if (existingNames.Any(existingName => IngredientNameNormalizer.Normalize(existingName) == normalized))
        {
            return;
        }

        db.PantryItems.Add(new PantryItem
        {
            Name = name,
            Category = NormalizeCategory(request.Category)
        });
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, PantryItemEditRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.PantryItems.FindAsync(id);
        if (item is null)
        {
            return;
        }

        item.Name = name;
        item.Category = NormalizeCategory(request.Category);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.PantryItems.FindAsync(id);
        if (item is null)
        {
            return;
        }

        db.PantryItems.Remove(item);
        await db.SaveChangesAsync();
    }

    private static string NormalizeCategory(string category) =>
        ShoppingCategory.DisplayOrder.Contains(category)
            ? category
            : ShoppingCategory.Other;
}
