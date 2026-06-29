using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class PlannerService(BrowserAppStore store)
{
    public async Task<List<DayPlan>> GetWeekAsync(DateOnly date)
    {
        var state = await store.GetStateAsync();
        var weekStart = WeekStart(date);
        EnsureWeekSlots(state, weekStart);

        return Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                return new DayPlan(
                    day,
                    store.CloneSlot(state, SlotFor(state, day, MealKind.Lunch)),
                    store.CloneSlot(state, SlotFor(state, day, MealKind.Dinner)));
            })
            .ToList();
    }

    public async Task<MealPlanSlot> GetSlotAsync(DateOnly date, MealKind mealKind)
    {
        var state = await store.GetStateAsync();
        EnsureWeekSlots(state, WeekStart(date));
        return store.CloneSlot(state, SlotFor(state, date, mealKind));
    }

    public async Task AddRecipeAsync(DateOnly date, MealKind mealKind, int recipeId, int plannedServings)
    {
        var state = await store.GetStateAsync();
        EnsureWeekSlots(state, WeekStart(date));
        var slot = SlotFor(state, date, mealKind);
        var nextOrder = slot.PlannedRecipes.Count == 0 ? 1 : slot.PlannedRecipes.Max(plannedRecipe => plannedRecipe.Order) + 1;
        var recipe = store.FindRecipe(state, recipeId);

        slot.PlannedRecipes.Add(new MealPlanRecipe
        {
            Id = store.NextId(state),
            MealPlanSlotId = slot.Id,
            RecipeId = recipeId,
            Recipe = recipe,
            PlannedServings = plannedServings,
            Order = nextOrder
        });

        await store.SaveAsync();
    }

    public async Task<MealPlanRecipe?> GetPlannedRecipeAsync(int plannedRecipeId)
    {
        var state = await store.GetStateAsync();
        var plannedRecipe = state.MealPlanSlots
            .SelectMany(slot => slot.PlannedRecipes)
            .FirstOrDefault(recipe => recipe.Id == plannedRecipeId);
        if (plannedRecipe is null)
        {
            return null;
        }

        var recipe = store.FindRecipe(state, plannedRecipe.RecipeId);
        return new MealPlanRecipe
        {
            Id = plannedRecipe.Id,
            MealPlanSlotId = plannedRecipe.MealPlanSlotId,
            RecipeId = plannedRecipe.RecipeId,
            PlannedServings = plannedRecipe.PlannedServings,
            Order = plannedRecipe.Order,
            Recipe = recipe is null ? null : store.CloneRecipe(recipe)
        };
    }

    public async Task RemoveRecipeAsync(int plannedRecipeId)
    {
        var state = await store.GetStateAsync();
        foreach (var slot in state.MealPlanSlots)
        {
            var plannedRecipe = slot.PlannedRecipes.FirstOrDefault(recipe => recipe.Id == plannedRecipeId);
            if (plannedRecipe is null)
            {
                continue;
            }

            slot.PlannedRecipes.Remove(plannedRecipe);
            await store.SaveAsync();
            return;
        }
    }

    public static DateOnly WeekStart(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(offset);
    }

    private void EnsureWeekSlots(LocalAppState state, DateOnly weekStart)
    {
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = weekStart.AddDays(dayOffset);
            foreach (var mealKind in new[] { MealKind.Lunch, MealKind.Dinner })
            {
                if (state.MealPlanSlots.Any(slot => slot.Date == date && slot.MealKind == mealKind))
                {
                    continue;
                }

                state.MealPlanSlots.Add(new MealPlanSlot
                {
                    Id = store.NextId(state),
                    Date = date,
                    MealKind = mealKind
                });
            }
        }
    }

    private static MealPlanSlot SlotFor(LocalAppState state, DateOnly date, MealKind mealKind) =>
        state.MealPlanSlots.Single(slot => slot.Date == date && slot.MealKind == mealKind);
}

public sealed record DayPlan(DateOnly Date, MealPlanSlot Lunch, MealPlanSlot Dinner);
