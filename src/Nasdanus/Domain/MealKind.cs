namespace Nasdanus.Domain;

public enum MealKind
{
    Lunch = 1,
    Dinner = 2,
    Breakfast = 3
}

public static class MealKindExtensions
{
    public static readonly MealKind[] PlanningOrder =
    [
        MealKind.Breakfast,
        MealKind.Lunch,
        MealKind.Dinner
    ];

    public static string ToDisplayName(this MealKind mealKind) => mealKind switch
    {
        MealKind.Breakfast => "Esmorzar",
        MealKind.Lunch => "Dinar",
        MealKind.Dinner => "Sopar",
        _ => mealKind.ToString()
    };

    public static bool MatchesRecipeCategory(this MealKind mealKind, string categoryText)
    {
        if (string.IsNullOrWhiteSpace(categoryText))
        {
            return false;
        }

        return mealKind switch
        {
            MealKind.Breakfast =>
                RecipeCategory.Contains(categoryText, RecipeCategory.Breakfast)
                || RecipeCategory.Contains(categoryText, RecipeCategory.BreakfastSnack)
                || RecipeCategory.Contains(categoryText, mealKind.ToString()),
            _ =>
                RecipeCategory.Contains(categoryText, mealKind.ToDisplayName())
                || RecipeCategory.Contains(categoryText, mealKind.ToString())
        };
    }
}
