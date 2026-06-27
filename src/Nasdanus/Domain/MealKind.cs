namespace Nasdanus.Domain;

public enum MealKind
{
    Lunch = 1,
    Dinner = 2
}

public static class MealKindExtensions
{
    public static string ToDisplayName(this MealKind mealKind) => mealKind switch
    {
        MealKind.Lunch => "Dinar",
        MealKind.Dinner => "Sopar",
        _ => mealKind.ToString()
    };
}
