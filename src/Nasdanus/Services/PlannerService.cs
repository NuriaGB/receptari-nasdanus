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
            .Include(slot => slot.Recipe)
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
            .Include(slot => slot.Recipe)
            .AsNoTracking()
            .SingleAsync(slot => slot.Date == date && slot.MealKind == mealKind);
    }

    public async Task AssignRecipeAsync(DateOnly date, MealKind mealKind, int? recipeId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await EnsureWeekSlotsAsync(db, WeekStart(date));

        var slot = await db.MealPlanSlots.SingleAsync(slot => slot.Date == date && slot.MealKind == mealKind);
        slot.RecipeId = recipeId;
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
