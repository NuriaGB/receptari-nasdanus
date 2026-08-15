using RecetariBlazor.Models;

namespace RecetariBlazor.Services;

public enum UnitScalingBehavior { Continuous, WholeUnit, Approximate, NoQuantity }

public record ScaledIngredient(
    Ingredient Original,
    double     NewQuantityRaw,
    double     NewQuantityAdjusted,
    string     NewQuantityDisplay,
    string?    WarningMessage,
    bool       WasAdjusted
);

public record ScalingPreview(
    double                    Factor,
    List<ScaledIngredient>    Ingredients,
    bool                      NeedsConfirmation,
    string?                   GlobalWarning
);

public static class IngredientScalingService
{
    private static readonly HashSet<string> Continuous  = new(StringComparer.OrdinalIgnoreCase)
        { "g","gr","gram","grams","kg","kilogram","ml","mililitre","mililitres","mililitro","mililitros","l","litre","litres","litro","litros","cl","mg" };
    private static readonly HashSet<string> Approximate = new(StringComparer.OrdinalIgnoreCase)
        { "cullerada","cullerades","cucharada","cucharadas","tbsp","cullereta","cucharadita","tsp","polsim","pizca","pinch","got","vaso","tassa","taza","cup","raig","chorro" };
    private static readonly HashSet<string> NoQtyKw     = new(StringComparer.OrdinalIgnoreCase)
        { "al gust","a gust","al gusto","c/s","cs","q.b.","qb","",  " " };

    public static UnitScalingBehavior ClassifyUnit(string unit, string quantity)
    {
        var q = quantity.Trim();
        if (string.IsNullOrWhiteSpace(q) || NoQtyKw.Contains(q) || !TryParseQuantity(q, out _))
            return UnitScalingBehavior.NoQuantity;
        if (Continuous.Contains(unit.Trim()))  return UnitScalingBehavior.Continuous;
        if (Approximate.Contains(unit.Trim())) return UnitScalingBehavior.Approximate;
        return UnitScalingBehavior.WholeUnit;
    }

    public static bool TryParseQuantity(string quantity, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(quantity)) return false;
        var parts = quantity.Trim().Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double n) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d) && d != 0)
        { value = n / d; return true; }
        return double.TryParse(quantity.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static (double v, string u) ToBase(double v, string u) =>
        u.ToLowerInvariant() switch { "kg" => (v * 1000, "g"), "l" => (v * 1000, "ml"), _ => (v, u) };
    private static (double v, string u) BestUnit(double v, string u) =>
        u.ToLowerInvariant() switch { "g" when v >= 1000 => (v / 1000, "kg"), "ml" when v >= 1000 => (v / 1000, "l"), _ => (v, u) };

    private static string Fmt(double v)
    {
        if (v == Math.Floor(v)) return ((int)v).ToString();
        foreach (var (fv, fs) in new[] { (0.25,"1/4"),(0.5,"1/2"),(0.75,"3/4"),(1.5,"1½"),(2.5,"2½") })
            if (Math.Abs(v - fv) < 0.05) return fs;
        return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double RoundCont(double v) =>
        v < 5 ? Math.Round(v, 1) :
        v < 20 ? Math.Round(v / 0.5) * 0.5 :
        v < 100 ? Math.Round(v / 5) * 5 :
        v < 500 ? Math.Round(v / 10) * 10 :
        Math.Round(v / 25) * 25;

    public static double? ComputeFactor(Ingredient reference, double newValue)
    {
        if (!TryParseQuantity(reference.Quantity, out double orig) || orig == 0) return null;
        var (normOrig, _) = ToBase(orig, reference.Unit);
        var (normNew,  _) = ToBase(newValue, reference.Unit);
        return normNew / normOrig;
    }

    public static ScalingPreview ComputeScaling(List<Ingredient> ingredients, Ingredient reference, double newValue)
    {
        if (ComputeFactor(reference, newValue) is not double factor)
            return new ScalingPreview(1, new(), false, "No s'ha pogut calcular el factor.");

        var results   = new List<ScaledIngredient>();
        bool needConf = false;
        string? globalWarn = null;

        if (Math.Abs(Math.Round(factor, 4) - Math.Round(Math.Round(factor, 2), 2)) > 0.005)
            globalWarn = $"El factor ({factor:0.####}) no és exacte. Alguns ingredients s'arrodoniran.";

        foreach (var ing in ingredients)
        {
            var behavior = ClassifyUnit(ing.Unit, ing.Quantity);
            if (behavior == UnitScalingBehavior.NoQuantity)
            { results.Add(new(ing, 0, 0, ing.Quantity, $"'{ing.Name}' sense quantitat → es manté.", false)); continue; }
            if (!TryParseQuantity(ing.Quantity, out double origVal))
            { results.Add(new(ing, 0, 0, ing.Quantity, $"'{ing.Name}': no s'ha pogut interpretar → es manté.", false)); continue; }

            var (normVal, normUnit) = ToBase(origVal, ing.Unit);
            double rawNew = normVal * factor;
            double adjusted; string? warn = null; bool wasAdj = false;

            switch (behavior)
            {
                case UnitScalingBehavior.Continuous:
                    adjusted = RoundCont(rawNew);
                    if (Math.Abs(adjusted - rawNew) > rawNew * 0.02) { warn = $"'{ing.Name}' arrodonit de {rawNew:0.##} a {adjusted} {normUnit}."; wasAdj = true; }
                    break;
                case UnitScalingBehavior.WholeUnit:
                    double rw = Math.Ceiling(rawNew);
                    if (Math.Abs(rw - rawNew) > 0.1)
                    { warn = $"'{ing.Name}' hauria de ser {rawNew:0.##} u. → arrodonit a {(int)rw}."; wasAdj = true; needConf = true; }
                    adjusted = rw;
                    break;
                default:
                    adjusted = Math.Round(rawNew, 1); break;
            }

            var (fv, fu) = BestUnit(adjusted, normUnit);
            results.Add(new(ing, rawNew, adjusted, $"{Fmt(fv)} {fu}".Trim(), warn, wasAdj));
        }

        return new ScalingPreview(factor, results, needConf || results.Any(r => r.WasAdjusted), globalWarn);
    }
}
