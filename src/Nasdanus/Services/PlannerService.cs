using Microsoft.EntityFrameworkCore;
using Nasdanus.Data;
using Nasdanus.Domain;

namespace Nasdanus.Services;

public sealed class PlannerService(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task<List<DayPlan>> GetWeekAsync(DateOnly date)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var weekStart = WeekStart(date);
        await EnsureWeekSlotsAsync(db, weekStart);

        var weekEnd = weekStart.AddDays(6);
        var slots = await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes.OrderBy(plannedRecipe => plannedRecipe.Order))
                .ThenInclude(plannedRecipe => plannedRecipe.Recipe)
            .Where(slot => slot.Date >= weekStart && slot.Date <= weekEnd)
            .AsNoTracking()
            .ToListAsync();

        return Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                return new DayPlan(
                    day,
                    slots.Single(slot => slot.Date == day && slot.MealKind == MealKind.Lunch),
                    slots.Single(slot => slot.Date == day && slot.MealKind == MealKind.Dinner));
            })
            .ToList();
    }

    public async Task<MealPlanSlot> GetSlotAsync(DateOnly date, MealKind mealKind)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureWeekSlotsAsync(db, WeekStart(date));

        return await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes.OrderBy(plannedRecipe => plannedRecipe.Order))
                .ThenInclude(plannedRecipe => plannedRecipe.Recipe)
            .AsNoTracking()
            .SingleAsync(slot => slot.Date == date && slot.MealKind == mealKind);
    }

    public async Task AddRecipeAsync(DateOnly date, MealKind mealKind, int recipeId, int plannedServings)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureWeekSlotsAsync(db, WeekStart(date));

        var slot = await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes)
            .SingleAsync(slot => slot.Date == date && slot.MealKind == mealKind);

        var nextOrder = slot.PlannedRecipes.Count == 0 ? 1 : slot.PlannedRecipes.Max(plannedRecipe => plannedRecipe.Order) + 1;
        slot.PlannedRecipes.Add(new MealPlanRecipe
        {
            RecipeId = recipeId,
            PlannedServings = plannedServings,
            Order = nextOrder
        });

        await db.SaveChangesAsync();
    }

    public async Task<MealPlanRecipe?> GetPlannedRecipeAsync(int plannedRecipeId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.MealPlanRecipes
            .Include(plannedRecipe => plannedRecipe.Recipe)
            .AsNoTracking()
            .FirstOrDefaultAsync(plannedRecipe => plannedRecipe.Id == plannedRecipeId);
    }

    public async Task RemoveRecipeAsync(int plannedRecipeId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var plannedRecipe = await db.MealPlanRecipes.FindAsync(plannedRecipeId);
        if (plannedRecipe is null)
        {
            return;
        }

        db.MealPlanRecipes.Remove(plannedRecipe);
        await db.SaveChangesAsync();
    }

    public static DateOnly WeekStart(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(offset);
    }

    private static async Task EnsureWeekSlotsAsync(NasdanusDbContext db, DateOnly weekStart)
    {
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = weekStart.AddDays(dayOffset);
            foreach (var mealKind in new[] { MealKind.Lunch, MealKind.Dinner })
            {
                var exists = await db.MealPlanSlots.AnyAsync(slot => slot.Date == date && slot.MealKind == mealKind);
                if (!exists)
                {
                    db.MealPlanSlots.Add(new MealPlanSlot
                    {
                        Date = date,
                        MealKind = mealKind
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }
}

public sealed record DayPlan(DateOnly Date, MealPlanSlot Lunch, MealPlanSlot Dinner);
