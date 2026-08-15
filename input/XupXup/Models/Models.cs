using System.Text.Json.Serialization;

namespace RecetariBlazor.Models;

// ─── Recepta ──────────────────────────────────────────────────────────────────

public class Recipe
{
    public string   Id              { get; set; } = Guid.NewGuid().ToString();
    public string   Name            { get; set; } = string.Empty;
    public string   Description     { get; set; } = string.Empty;
    public int      PrepTimeMinutes { get; set; }
    public int      CookTimeMinutes { get; set; }
    public int      Servings        { get; set; } = 4;
    public int      Difficulty      { get; set; } = 1;  // 1=Fàcil 2=Mitjana 3=Difícil
    public string   ImageBase64     { get; set; } = string.Empty;
    public string   Tips            { get; set; } = string.Empty;
    public DateTime CreatedAt       { get; set; } = DateTime.Now;
    public DateTime UpdatedAt       { get; set; } = DateTime.Now;
    public List<string>      CategoryIds { get; set; } = new();
    public List<Ingredient>  Ingredients { get; set; } = new();
    public List<Equipment>   Equipment   { get; set; } = new();
    public List<RecipeStep>  Steps       { get; set; } = new();

    [JsonIgnore]
    public string DifficultyText => Difficulty switch { 1 => "Fàcil", 2 => "Mitjana", 3 => "Difícil", _ => "" };

    [JsonIgnore]
    public string TotalTimeText
    {
        get
        {
            var t = PrepTimeMinutes + CookTimeMinutes;
            if (t == 0) return "Sense especificar";
            return t < 60 ? $"{t} min" : $"{t / 60}h {t % 60:00}min";
        }
    }
}

public class Ingredient
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = string.Empty;
    public string Quantity    { get; set; } = string.Empty;
    public string Unit        { get; set; } = string.Empty;
    public int    Order       { get; set; }
    public string CategoriaId { get; set; } = string.Empty; // de la BD d'ingredients

    [JsonIgnore]
    public string DisplayText =>
        string.IsNullOrWhiteSpace(Quantity) ? Name : $"{Quantity} {Unit} {Name}".Trim();
}

public class Equipment
{
    public string Id    { get; set; } = Guid.NewGuid().ToString();
    public string Name  { get; set; } = string.Empty;
    public int    Order { get; set; }
}

/// <summary>Ingredient linkat a un pas — associa un text visible amb un ingredient.</summary>
public class StepIngredientLink
{
    public string IngredientId   { get; set; } = string.Empty;
    public string DisplayText    { get; set; } = string.Empty; // el text que ha escrit l'usuari
}

public class RecipeStep
{
    public string Id             { get; set; } = Guid.NewGuid().ToString();
    public int    StepNumber     { get; set; }
    public string Description    { get; set; } = string.Empty; // text net (fallback/cerca)
    public string DescriptionHtml{ get; set; } = string.Empty; // HTML rich text (contenteditable)
    public int?   TimerMinutes   { get; set; }
    public string ImageBase64    { get; set; } = string.Empty; // foto opcional
    public List<StepIngredientLink> Links { get; set; } = new();

    [JsonIgnore] public bool   IsDone    { get; set; }
    [JsonIgnore] public string StepLabel => $"Pas {StepNumber}";

    /// <summary>Sincronitza Description (text pla) des de DescriptionHtml.</summary>
    public void SyncPlainText()
    {
        if (string.IsNullOrEmpty(DescriptionHtml)) return;
        Description = System.Text.RegularExpressions.Regex.Replace(
            DescriptionHtml, "<[^>]+>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Trim();
    }
}

// ─── Categoria ────────────────────────────────────────────────────────────────

public class Category
{
    public string Id       { get; set; } = Guid.NewGuid().ToString();
    public string Name     { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#888888";
    public string Emoji    { get; set; } = "🍽️";

    [JsonIgnore] public string DisplayName => $"{Emoji} {Name}";
}

// ─── Menú ─────────────────────────────────────────────────────────────────────

public enum MealSlotType { Esmorzar = 0, MigMati = 1, Dinar = 2, Berenar = 3, Sopar = 4 }

public static class MealSlotExtensions
{
    public static string ToDisplay(this MealSlotType s) => s switch
    {
        MealSlotType.Esmorzar => "☀️ Esmorzar",
        MealSlotType.MigMati  => "🥐 Mig matí",
        MealSlotType.Dinar    => "🍽️ Dinar",
        MealSlotType.Berenar  => "🍎 Berenar",
        MealSlotType.Sopar    => "🌙 Sopar",
        _                     => s.ToString()
    };
}

public class MenuPlan
{
    public string   Id        { get; set; } = Guid.NewGuid().ToString();
    public string   Name      { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate   { get; set; } = DateTime.Today.AddDays(6);
    public string   Notes     { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<MenuDay> Days { get; set; } = new();

    [JsonIgnore] public string DateRangeText => $"{StartDate:dd MMM} – {EndDate:dd MMM yyyy}";
    [JsonIgnore] public int    TotalRecipes  => Days.Sum(d => d.Slots.Count(s => s.HasRecipe));
}

public class MenuDay
{
    public string      Id        { get; set; } = Guid.NewGuid().ToString();
    public DateTime    Date      { get; set; }
    public List<MenuSlot> Slots  { get; set; } = new();

    [JsonIgnore] public string DayLabel => Date.ToString("dddd d MMM", new System.Globalization.CultureInfo("ca-ES"));
    [JsonIgnore] public bool   IsToday  => Date.Date == DateTime.Today;
}

public class MenuSlot
{
    public string       Id         { get; set; } = Guid.NewGuid().ToString();
    public MealSlotType SlotType   { get; set; }
    public List<string> RecipeIds  { get; set; } = new();
    // Kept for migration of old single-recipe data; ignored after migration
    public string?      RecipeId   { get; set; }
    public string       Notes      { get; set; } = string.Empty;

    [JsonIgnore] public List<Recipe> Recipes   { get; set; } = new();
    [JsonIgnore] public string       SlotLabel => SlotType.ToDisplay();
    [JsonIgnore] public bool         HasRecipe => RecipeIds.Count > 0;
    [JsonIgnore] public string       RecipeName =>
        Recipes.Count > 0 ? string.Join(", ", Recipes.Select(r => r.Name)) : "— sense recepta —";
}

// ─── Contenidor de dades (el fitxer JSON al Drive) ────────────────────────────

/// <summary>Ingredient personalitzat afegit per l'usuari, persistit al Drive.</summary>
public class CustomIngredientEntry
{
    public string       Id        { get; set; } = Guid.NewGuid().ToString();
    public string       Nom       { get; set; } = string.Empty;
    public string       Categoria { get; set; } = "altres";
    public List<string> Sinonims  { get; set; } = new();
}

public class AppData
{
    public List<Recipe>   Recipes    { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<MenuPlan> Menus             { get; set; } = new();
    public List<CustomIngredientEntry> CustomIngredients { get; set; } = new();
    public DateTime       LastSaved         { get; set; } = DateTime.Now;
    public string         Version           { get; set; } = "1.0";
}
