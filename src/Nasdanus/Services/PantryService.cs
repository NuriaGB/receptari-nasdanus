using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class PantryService(BrowserAppStore store)
{
    public async Task<List<PantryItem>> GetAllAsync()
    {
        var state = await store.GetStateAsync();
        return state.PantryItems
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Name)
            .Select(item => new PantryItem
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToList();
    }

    public async Task AddAsync(PantryItemEditRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var state = await store.GetStateAsync();
        var normalized = IngredientNameNormalizer.Normalize(name);
        if (state.PantryItems.Any(item => IngredientNameNormalizer.Normalize(item.Name) == normalized))
        {
            return;
        }

        state.PantryItems.Add(new PantryItem
        {
            Id = store.NextId(state),
            Name = name,
            Category = NormalizeCategory(request.Category)
        });
        await store.SaveAsync();
    }

    public async Task UpdateAsync(int id, PantryItemEditRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var state = await store.GetStateAsync();
        var item = state.PantryItems.FirstOrDefault(pantryItem => pantryItem.Id == id);
        if (item is null)
        {
            return;
        }

        item.Name = name;
        item.Category = NormalizeCategory(request.Category);
        item.UpdatedAt = DateTime.UtcNow;
        await store.SaveAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var state = await store.GetStateAsync();
        var item = state.PantryItems.FirstOrDefault(pantryItem => pantryItem.Id == id);
        if (item is null)
        {
            return;
        }

        state.PantryItems.Remove(item);
        await store.SaveAsync();
    }

    private static string NormalizeCategory(string category) =>
        ShoppingCategory.DisplayOrder.Contains(category)
            ? category
            : ShoppingCategory.Other;
}
