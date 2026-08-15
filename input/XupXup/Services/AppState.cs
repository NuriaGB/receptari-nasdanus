using RecetariBlazor.Models;

namespace RecetariBlazor.Services;

/// <summary>
/// Estat central de l'aplicació. Tots els components obtenen i modifiquen
/// les dades a través d'aquest servei.
/// </summary>
public class AppState
{
    private readonly GoogleDriveService  _drive;
    private readonly IngredientDbService _ingredientDb;
    private AppData _data = new();

    public bool        IsLoaded     { get; private set; }
    public bool        IsSaving     { get; private set; }
    public string?     ErrorMessage { get; private set; }

    // Col·leccions exposades
    public IReadOnlyList<Recipe>   Recipes    => _data.Recipes;
    public IReadOnlyList<Category> Categories => _data.Categories;
    public IReadOnlyList<MenuPlan> Menus      => _data.Menus;
    public AppData Data => _data;

    public event Action? StateChanged;

    public AppState(GoogleDriveService drive, IngredientDbService ingredientDb)
    {
        _drive        = drive;
        _ingredientDb = ingredientDb;
        _drive.AuthStateChanged += OnAuthChanged;
    }

    private async void OnAuthChanged()
    {
        if (_drive.IsAuthenticated)
            await LoadAsync();
        else
        {
            _data    = new AppData();
            IsLoaded = false;
            Notify();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = null;
            _data        = await _drive.LoadDataAsync();
            IsLoaded     = true;
            MigrateSlots();
            RehydrateMenuRecipes();
            if (_data.CustomIngredients.Any())
                _ingredientDb.LoadCustomIngredients(_data.CustomIngredients);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error carregant dades: {ex.Message}";
        }
        Notify();
    }

    public async Task SaveAsync()
    {
        IsSaving = true;
        Notify();
        try
        {
            await _drive.SaveDataAsync(_data);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error desant: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            Notify();
        }
    }

    // ═══════════════════════════════════════════
    //  RECEPTES
    // ═══════════════════════════════════════════

    public Recipe? GetRecipe(string id) => _data.Recipes.FirstOrDefault(r => r.Id == id);

    public async Task SaveRecipeAsync(Recipe recipe)
    {
        recipe.UpdatedAt = DateTime.Now;
        var existing = _data.Recipes.FindIndex(r => r.Id == recipe.Id);
        if (existing >= 0) _data.Recipes[existing] = recipe;
        else               _data.Recipes.Add(recipe);
        await SaveAsync();
    }

    public async Task DeleteRecipeAsync(string id)
    {
        _data.Recipes.RemoveAll(r => r.Id == id);
        foreach (var plan in _data.Menus)
            foreach (var day in plan.Days)
                foreach (var slot in day.Slots)
                {
                    slot.RecipeIds.Remove(id);
                    slot.Recipes.RemoveAll(r => r.Id == id);
                }
        await SaveAsync();
    }

    public List<Recipe> SearchRecipes(string query, List<string>? categoryIds = null)
    {
        var results = _data.Recipes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
            results = results.Where(r =>
                r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (categoryIds is { Count: > 0 })
            results = results.Where(r => categoryIds.Any(c => r.CategoryIds.Contains(c)));
        return results.OrderBy(r => r.Name).ToList();
    }

    // ═══════════════════════════════════════════
    //  CATEGORIES
    // ═══════════════════════════════════════════

    public async Task SaveCategoryAsync(Category cat)
    {
        var existing = _data.Categories.FindIndex(c => c.Id == cat.Id);
        if (existing >= 0) _data.Categories[existing] = cat;
        else               _data.Categories.Add(cat);
        await SaveAsync();
    }

    public async Task DeleteCategoryAsync(string id)
    {
        _data.Categories.RemoveAll(c => c.Id == id);
        foreach (var r in _data.Recipes) r.CategoryIds.Remove(id);
        await SaveAsync();
    }

    // ═══════════════════════════════════════════
    //  MENÚS
    // ═══════════════════════════════════════════

    public MenuPlan? GetMenu(string id) => _data.Menus.FirstOrDefault(m => m.Id == id);

    public async Task SaveMenuAsync(MenuPlan plan)
    {
        // Migrate old single RecipeId → RecipeIds
        foreach (var slot in plan.Days.SelectMany(d => d.Slots))
        {
            if (slot.RecipeId != null && !slot.RecipeIds.Contains(slot.RecipeId))
                slot.RecipeIds.Add(slot.RecipeId);
            slot.RecipeId = null;
        }
        plan.Days.ForEach(d => d.Slots.ForEach(s => s.Recipes = new())); // no serialitzar refs circulars
        var existing = _data.Menus.FindIndex(m => m.Id == plan.Id);
        if (existing >= 0) _data.Menus[existing] = plan;
        else               _data.Menus.Add(plan);
        await SaveAsync();
        // Rehidratar
        var recipeMap = _data.Recipes.ToDictionary(r => r.Id);
        foreach (var day in plan.Days)
            foreach (var slot in day.Slots)
                slot.Recipes = slot.RecipeIds
                    .Select(id => recipeMap.TryGetValue(id, out var r) ? r : null)
                    .Where(r => r != null).Cast<Recipe>().ToList();
    }

    public async Task DeleteMenuAsync(string id)
    {
        _data.Menus.RemoveAll(m => m.Id == id);
        await SaveAsync();
    }

    private void MigrateSlots()
    {
        foreach (var slot in _data.Menus.SelectMany(m => m.Days).SelectMany(d => d.Slots))
        {
            if (slot.RecipeId != null && !slot.RecipeIds.Contains(slot.RecipeId))
                slot.RecipeIds.Add(slot.RecipeId);
            slot.RecipeId = null;
        }
    }

    private void RehydrateMenuRecipes()
    {
        var recipeMap = _data.Recipes.ToDictionary(r => r.Id);
        foreach (var slot in _data.Menus.SelectMany(m => m.Days).SelectMany(d => d.Slots))
            slot.Recipes = slot.RecipeIds
                .Select(id => recipeMap.TryGetValue(id, out var r) ? r : null)
                .Where(r => r != null).Cast<Recipe>().ToList();
    }

    private void Notify() => StateChanged?.Invoke();
}
