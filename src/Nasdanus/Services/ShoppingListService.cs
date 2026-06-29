using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nasdanus.Data;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class ShoppingListService(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task<ShoppingList> GetWeekAsync(DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = PlannerService.WeekStart(date);
        var list = await GetOrCreateListAsync(db, weekStart);

        if (list.Items.Count == 0)
        {
            await RegenerateItemsAsync(db, list, keepManualItems: true);
            list = await LoadListAsync(db, weekStart);
        }

        return list;
    }

    public async Task<ShoppingList> RegenerateWeekAsync(DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = PlannerService.WeekStart(date);
        var list = await GetOrCreateListAsync(db, weekStart);
        await RegenerateItemsAsync(db, list, keepManualItems: true);
        return await LoadListAsync(db, weekStart);
    }

    public async Task SetCheckedAsync(int itemId, bool isChecked)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems
            .Include(shoppingItem => shoppingItem.ShoppingList)
            .FirstOrDefaultAsync(shoppingItem => shoppingItem.Id == itemId);
        if (item is null)
        {
            return;
        }

        item.IsChecked = isChecked;
        item.ShoppingList!.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllPurchasedAsync(DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = PlannerService.WeekStart(date);
        var list = await db.ShoppingLists
            .Include(shoppingList => shoppingList.Items)
            .FirstOrDefaultAsync(shoppingList => shoppingList.WeekStart == weekStart);
        if (list is null)
        {
            return;
        }

        foreach (var item in list.Items)
        {
            item.IsChecked = true;
        }

        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ClearPurchasedItemsAsync(DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = PlannerService.WeekStart(date);
        var list = await db.ShoppingLists
            .Include(shoppingList => shoppingList.Items)
            .FirstOrDefaultAsync(shoppingList => shoppingList.WeekStart == weekStart);
        if (list is null)
        {
            return;
        }

        var purchasedItems = list.Items.Where(item => item.IsChecked).ToList();
        db.ShoppingListItems.RemoveRange(purchasedItems);
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task AddManualItemAsync(DateOnly date, ShoppingItemEditRequest request)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = PlannerService.WeekStart(date);
        var list = await GetOrCreateListAsync(db, weekStart);

        list.Items.Add(new ShoppingListItem
        {
            Name = request.Name.Trim(),
            Category = NormalizeCategory(request.Category),
            QuantityText = request.QuantityText.Trim(),
            Unit = NormalizeUnit(request.Unit.Trim()),
            Quantity = IngredientScaling.ParseQuantity(request.QuantityText),
            SourceRecipeCount = 0,
            SourceRecipeNames = string.Empty,
            RecipeId = request.IsHouseholdItem ? null : request.RecipeId,
            IsHouseholdItem = request.IsHouseholdItem,
            IsManual = true,
            Order = NextOrder(list)
        });
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(int itemId, ShoppingItemEditRequest request)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems
            .Include(shoppingItem => shoppingItem.ShoppingList)
            .FirstOrDefaultAsync(shoppingItem => shoppingItem.Id == itemId);
        if (item is null)
        {
            return;
        }

        item.Name = request.Name.Trim();
        item.Category = NormalizeCategory(request.Category);
        item.QuantityText = request.QuantityText.Trim();
        item.Unit = NormalizeUnit(request.Unit.Trim());
        item.Quantity = IngredientScaling.ParseQuantity(request.QuantityText);
        item.RecipeId = request.IsHouseholdItem ? null : request.RecipeId;
        item.IsHouseholdItem = request.IsHouseholdItem;
        item.ShoppingList!.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int itemId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems
            .Include(shoppingItem => shoppingItem.ShoppingList)
            .FirstOrDefaultAsync(shoppingItem => shoppingItem.Id == itemId);
        if (item is null)
        {
            return;
        }

        item.ShoppingList!.UpdatedAt = DateTime.UtcNow;
        db.ShoppingListItems.Remove(item);
        await db.SaveChangesAsync();
    }

    private static async Task<ShoppingList> GetOrCreateListAsync(NasdanusDbContext db, DateOnly weekStart)
    {
        var list = await db.ShoppingLists
            .Include(shoppingList => shoppingList.Items.OrderBy(item => item.Order))
                .ThenInclude(item => item.Recipe)
            .FirstOrDefaultAsync(shoppingList => shoppingList.WeekStart == weekStart);

        if (list is not null)
        {
            return list;
        }

        list = new ShoppingList
        {
            WeekStart = weekStart
        };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync();
        return list;
    }

    private static async Task<ShoppingList> LoadListAsync(NasdanusDbContext db, DateOnly weekStart) =>
        await db.ShoppingLists
            .Include(shoppingList => shoppingList.Items.OrderBy(item => item.Order))
                .ThenInclude(item => item.Recipe)
            .AsNoTracking()
            .SingleAsync(shoppingList => shoppingList.WeekStart == weekStart);

    private static async Task RegenerateItemsAsync(NasdanusDbContext db, ShoppingList list, bool keepManualItems)
    {
        var generatedItems = await GenerateFromPlannerAsync(db, list.WeekStart);
        var generatedExistingItems = list.Items.Where(item => !item.IsManual).ToList();
        db.ShoppingListItems.RemoveRange(generatedExistingItems);

        if (!keepManualItems)
        {
            db.ShoppingListItems.RemoveRange(list.Items.Where(item => item.IsManual));
            list.Items.Clear();
        }

        var manualCount = keepManualItems
            ? list.Items.Count(item => item.IsManual)
            : 0;
        for (var index = 0; index < generatedItems.Count; index++)
        {
            generatedItems[index].ShoppingListId = list.Id;
            generatedItems[index].Order = manualCount + index + 1;
            list.Items.Add(generatedItems[index]);
        }

        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task<List<ShoppingListItem>> GenerateFromPlannerAsync(NasdanusDbContext db, DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        var slots = await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes)
                .ThenInclude(plannedRecipe => plannedRecipe.Recipe)
                    .ThenInclude(recipe => recipe!.Ingredients)
            .Where(slot => slot.Date >= weekStart && slot.Date <= weekEnd)
            .AsNoTracking()
            .ToListAsync();
        var pantryIngredientNames = await db.PantryItems
            .Select(item => item.Name)
            .AsNoTracking()
            .ToListAsync();
        var pantryIngredientKeys = pantryIngredientNames
            .Select(IngredientNameNormalizer.Normalize)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var aggregates = new Dictionary<string, ShoppingItemAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var plannedRecipe in slots.SelectMany(slot => slot.PlannedRecipes))
        {
            if (plannedRecipe.Recipe is null)
            {
                continue;
            }

            var plannedServings = plannedRecipe.PlannedServings > 0
                ? plannedRecipe.PlannedServings
                : plannedRecipe.Recipe.Servings;
            var scale = IngredientScaling.ScaleFactor(plannedRecipe.Recipe.Servings, plannedServings);

            foreach (var ingredient in plannedRecipe.Recipe.Ingredients.OrderBy(ingredient => ingredient.Order))
            {
                AddIngredient(aggregates, ingredient, scale, pantryIngredientKeys, plannedRecipe.Recipe.Name);
            }
        }

        return aggregates.Values
            .OrderBy(aggregate => CategoryOrder(aggregate.Category))
            .ThenBy(aggregate => aggregate.Name)
            .Select((aggregate, index) => aggregate.ToShoppingListItem(index + 1))
            .ToList();
    }

    private static void AddIngredient(
        Dictionary<string, ShoppingItemAggregate> aggregates,
        RecipeIngredient ingredient,
        decimal scale,
        IReadOnlySet<string> pantryIngredientKeys,
        string sourceRecipeName)
    {
        if (string.IsNullOrWhiteSpace(ingredient.Name))
        {
            return;
        }

        var name = ingredient.Name.Trim();
        if (IsInPantry(name, pantryIngredientKeys))
        {
            return;
        }

        var unit = NormalizeUnit(ingredient.Unit.Trim());
        var category = Categorize(name);
        var key = $"{IngredientNameNormalizer.Normalize(name)}|{category}";
        var quantity = ScaledQuantity(ingredient, scale);
        var quantityText = quantity is null
            ? ingredient.Quantity.Trim()
            : FormatQuantity(quantity.Value);

        if (!aggregates.TryGetValue(key, out var aggregate))
        {
            aggregate = new ShoppingItemAggregate(name, category, unit);
            aggregates.Add(key, aggregate);
        }

        aggregate.Add(quantity, quantityText, unit, sourceRecipeName);
    }

    private static decimal? ScaledQuantity(RecipeIngredient ingredient, decimal scale)
    {
        var quantity = IngredientScaling.ParseQuantity(ingredient.Quantity);
        if (quantity is null)
        {
            return null;
        }

        var effectiveScale = ingredient.ScalingMode is IngredientScalingMode.Fixed or IngredientScalingMode.ToTaste
            ? 1
            : scale;
        return quantity.Value * effectiveScale;
    }

    private static int NextOrder(ShoppingList list) =>
        list.Items.Count == 0 ? 1 : list.Items.Max(item => item.Order) + 1;

    private static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.##", CultureInfo.InvariantCulture);

    private static string NormalizeCategory(string category) =>
        ShoppingCategory.DisplayOrder.Contains(category)
            ? category
            : ShoppingCategory.Other;

    private static int CategoryOrder(string category)
    {
        var index = Array.IndexOf(ShoppingCategory.DisplayOrder, category);
        return index < 0 ? ShoppingCategory.DisplayOrder.Length : index;
    }

    private static bool IsInPantry(string ingredientName, IReadOnlySet<string> pantryIngredientKeys)
    {
        var normalized = IngredientNameNormalizer.Normalize(ingredientName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return pantryIngredientKeys.Contains(normalized)
            || pantryIngredientKeys.Any(pantryName =>
                normalized.StartsWith($"{pantryName} ", StringComparison.OrdinalIgnoreCase)
                || pantryName.StartsWith($"{normalized} ", StringComparison.OrdinalIgnoreCase));
    }

    private static string Categorize(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (ContainsAny(normalized, "pastanaga", "carbass", "alberg", "patata", "espinac", "tom", "ceba", "pebrot", "verdura", "enciam", "amanida"))
        {
            return ShoppingCategory.Vegetables;
        }

        if (ContainsAny(normalized, "pollastre", "vedella", "porc", "gall dindi", "carn", "pit de"))
        {
            return ShoppingCategory.Meat;
        }

        if (ContainsAny(normalized, "peix", "salm", "tonyina", "gamba", "muscl", "clo", "marisc", "bacall"))
        {
            return ShoppingCategory.Fish;
        }

        if (ContainsAny(normalized, "ou", "ous", "llet", "iogurt", "formatge", "mantega", "nata"))
        {
            return ShoppingCategory.DairyEggs;
        }

        if (ContainsAny(normalized, "sal", "pebre", "curc", "cÃºrc", "garam", "gingebre", "julivert", "herba", "jalape", "bitxo", "xili", "safra"))
        {
            return ShoppingCategory.Spices;
        }

        if (ContainsAny(normalized, "arr", "farina", "pasta", "cous", "blat", "pa", "sucre", "mel", "llevat", "brou", "soja", "oli", "vinagre", "llavor"))
        {
            return ShoppingCategory.Pantry;
        }

        return ShoppingCategory.Other;
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(value.Contains);

    private sealed class ShoppingItemAggregate(string name, string category, string firstUnit)
    {
        private readonly Dictionary<string, decimal> quantitiesByUnit = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> quantityTexts = [];
        private readonly SortedSet<string> sourceRecipeNames = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; } = name;
        public string Category { get; } = category;
        public string FirstUnit { get; } = firstUnit;
        private decimal? SingleQuantity => quantitiesByUnit.Count == 1 && quantityTexts.Count == 0
            ? quantitiesByUnit.Values.Single()
            : null;

        public void Add(decimal? quantity, string quantityText, string unit, string sourceRecipeName)
        {
            if (!string.IsNullOrWhiteSpace(sourceRecipeName))
            {
                sourceRecipeNames.Add(sourceRecipeName.Trim());
            }

            if (quantity is null)
            {
                if (!string.IsNullOrWhiteSpace(quantityText))
                {
                    quantityTexts.Add(AmountText(quantityText, unit));
                }

                return;
            }

            var key = string.IsNullOrWhiteSpace(unit) ? FirstUnit : unit;
            quantitiesByUnit[key] = quantitiesByUnit.TryGetValue(key, out var existing)
                ? existing + quantity.Value
                : quantity.Value;
        }

        public ShoppingListItem ToShoppingListItem(int order)
        {
            var quantity = SingleQuantity;
            var unit = quantity is null ? string.Empty : quantitiesByUnit.Keys.Single();
            var quantityText = quantity is null
                ? CombinedQuantityText()
                : FormatQuantity(quantity.Value);

            return new ShoppingListItem
            {
                Name = Name,
                Category = Category,
                Quantity = quantity,
                QuantityText = quantityText,
                Unit = DisplayUnit(unit, quantity),
                SourceRecipeCount = sourceRecipeNames.Count,
                SourceRecipeNames = string.Join(" · ", sourceRecipeNames.Take(2)),
                Order = order
            };
        }

        private string CombinedQuantityText()
        {
            var texts = quantitiesByUnit
                .OrderBy(pair => pair.Key)
                .Select(pair => AmountText(FormatQuantity(pair.Value), DisplayUnit(pair.Key, pair.Value)))
                .Concat(quantityTexts)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return string.Join(" + ", texts);
        }
    }

    private static string AmountText(string quantityText, string unit) =>
        string.Join(" ", new[] { quantityText, unit }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string NormalizeUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "u" => "unitat",
            "unitats" => "unitat",
            "cullerades" => "cullerada",
            "culleradetes" => "culleradeta",
            "grapats" => "grapat",
            "trossos" => "tros",
            "trossets" => "trosset",
            _ => normalized
        };
    }

    private static string DisplayUnit(string unit, decimal? quantity)
    {
        if (quantity is null || quantity.Value == 1)
        {
            return unit;
        }

        return unit switch
        {
            "unitat" => "unitats",
            "cullerada" => "cullerades",
            "culleradeta" => "culleradetes",
            "grapat" => "grapats",
            "tros" => "trossos",
            "trosset" => "trossets",
            _ => unit
        };
    }
}
