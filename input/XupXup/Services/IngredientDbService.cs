using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RecetariBlazor.Models;

namespace RecetariBlazor.Services;

// ─── Models de la BD ───────────────────────────────────────────────────────────

public class IngredientDbCategory
{
    [JsonPropertyName("id")]    public string Id    { get; set; } = string.Empty;
    [JsonPropertyName("nom")]   public string Nom   { get; set; } = string.Empty;
    [JsonPropertyName("emoji")] public string Emoji { get; set; } = string.Empty;
    [JsonPropertyName("ordre")] public int    Ordre { get; set; }
}

public class IngredientDbEntry
{
    [JsonPropertyName("id")]        public string       Id        { get; set; } = string.Empty;
    [JsonPropertyName("nom")]       public string       Nom       { get; set; } = string.Empty;
    [JsonPropertyName("categoria")] public string       Categoria { get; set; } = string.Empty;
    [JsonPropertyName("sinonims")]  public List<string> Sinonims  { get; set; } = new();
}

public class IngredientDatabase
{
    [JsonPropertyName("categories")] public List<IngredientDbCategory> Categories { get; set; } = new();
    [JsonPropertyName("ingredients")] public List<IngredientDbEntry>   Ingredients { get; set; } = new();
}

// ─── Resultat de cerca ─────────────────────────────────────────────────────────

public class IngredientMatch
{
    public IngredientDbEntry    Entry    { get; set; } = default!;
    public IngredientDbCategory Category { get; set; } = default!;
    public int                  Score    { get; set; } // 0-100, com de bona és la coincidència
}

// ─── Servei ────────────────────────────────────────────────────────────────────

public class IngredientDbService
{
    private readonly HttpClient _http;
    private IngredientDatabase? _db;
    private bool _loaded = false;

    public IngredientDbService(HttpClient http) => _http = http;

    public bool IsLoaded => _loaded;

    public List<IngredientDbCategory> Categories =>
        _db?.Categories.OrderBy(c => c.Ordre).ToList() ?? new();

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _db     = await _http.GetFromJsonAsync<IngredientDatabase>("data/ingredients-db.json");
        _loaded = true;
    }

    /// <summary>
    /// Busca a la BD si un nom d'ingredient coincideix amb algun entrada.
    /// Retorna null si no hi ha coincidència prou bona (score >= 60).
    /// </summary>
    public IngredientMatch? FindMatch(string nomIngredient)
    {
        if (_db == null || string.IsNullOrWhiteSpace(nomIngredient)) return null;

        var nom    = Normalize(nomIngredient);
        var catMap = _db.Categories.ToDictionary(c => c.Id);

        IngredientMatch? best = null;

        foreach (var entry in _db.Ingredients)
        {
            var score = ComputeScore(nom, entry);
            if (score > (best?.Score ?? 0) && score >= 60)
            {
                if (!catMap.TryGetValue(entry.Categoria, out var cat)) continue;
                best = new IngredientMatch { Entry = entry, Category = cat, Score = score };
            }
        }

        return best;
    }

    /// <summary>Autocomplete: retorna fins a 6 suggeriments mentre l'usuari escriu.</summary>
    public List<IngredientMatch> Autocomplete(string query, int max = 6)
    {
        if (_db == null || query.Length < 2) return new();

        var q      = Normalize(query);
        var catMap = _db.Categories.ToDictionary(c => c.Id);

        return _db.Ingredients
            .Select(e => new { e, score = ComputeScore(q, e) })
            .Where(x => x.score >= 40)
            .OrderByDescending(x => x.score)
            .Take(max)
            .Select(x => {
                catMap.TryGetValue(x.e.Categoria, out var cat);
                return new IngredientMatch { Entry = x.e, Category = cat!, Score = x.score };
            })
            .ToList();
    }

    /// <summary>Retorna la categoria d'un ingredient de recepta, o null si no es reconeix.</summary>
    public IngredientDbCategory? GetCategory(Ingredient ing)
    {
        var match = FindMatch(ing.Name);
        return match?.Category;
    }

    /// <summary>Afegeix un ingredient personalitzat de l'usuari a la BD en memòria.</summary>
    public void AddCustomIngredient(string nom, string categoriaId, List<string> sinonims)
    {
        _db ??= new IngredientDatabase();
        // Evitem duplicats
        if (_db.Ingredients.Any(i => Normalize(i.Nom) == Normalize(nom))) return;
        _db.Ingredients.Add(new IngredientDbEntry
        {
            Id        = "custom_" + Guid.NewGuid().ToString("N")[..8],
            Nom       = nom,
            Categoria = categoriaId,
            Sinonims  = sinonims
        });
    }

    /// <summary>Carrega els ingredients personalitzats de l'usuari (des d'AppData).</summary>
    public void LoadCustomIngredients(IEnumerable<RecetariBlazor.Models.CustomIngredientEntry> customs)
    {
        foreach (var c in customs)
            AddCustomIngredient(c.Nom, c.Categoria, c.Sinonims);
    }

    /// <summary>Retorna tots els ingredients (base + personalitzats) per mostrar a la pàgina de gestió.</summary>
    public List<IngredientDbEntry> GetAllEntries() =>
        _db?.Ingredients.ToList() ?? new();

    /// <summary>Actualitza nom, categoria i sinònims d'un ingredient (base o personalitzat).</summary>
    public void UpdateEntry(string id, string nom, string categoriaId, List<string> sinonims)
    {
        if (_db == null) return;
        var entry = _db.Ingredients.FirstOrDefault(i => i.Id == id);
        if (entry == null) return;
        entry.Nom       = nom;
        entry.Categoria = categoriaId;
        entry.Sinonims  = sinonims;
        // Si era de la BD base i l'hem modificat, el marquem com a custom
        if (!entry.Id.StartsWith("custom_"))
            entry.Id = "custom_" + entry.Id; // marca sobreescriptura
    }

    /// <summary>Retorna els ingredients personalitzats (per persistir a AppData).</summary>
    public List<RecetariBlazor.Models.CustomIngredientEntry> GetCustomIngredients()
    {
        if (_db == null) return new();
        return _db.Ingredients
            .Where(i => i.Id.StartsWith("custom_"))
            .Select(i => new RecetariBlazor.Models.CustomIngredientEntry
            {
                Nom       = i.Nom,
                Categoria = i.Categoria,
                Sinonims  = i.Sinonims
            })
            .ToList();
    }

    // ─── Helpers privats ──────────────────────────────────────────────────────

    private static string Normalize(string s) =>
        s.ToLowerInvariant()
         .Replace("à", "a").Replace("á", "a")
         .Replace("è", "e").Replace("é", "e")
         .Replace("ï", "i").Replace("í", "i")
         .Replace("ò", "o").Replace("ó", "o")
         .Replace("ú", "u").Replace("ü", "u")
         .Replace("ç", "c").Replace("·", "")
         .Trim();

    private static int ComputeScore(string query, IngredientDbEntry entry)
    {
        var nomN = Normalize(entry.Nom);

        // Coincidència exacta del nom principal
        if (nomN == query) return 100;

        // El nom principal conté la query o viceversa
        if (nomN.Contains(query)) return 85;
        if (query.Contains(nomN)) return 80;

        // Comença per la query
        if (nomN.StartsWith(query)) return 75;

        // Sinònims
        foreach (var sin in entry.Sinonims)
        {
            var sinN = Normalize(sin);
            if (sinN == query)           return 95;
            if (sinN.Contains(query))    return 70;
            if (query.Contains(sinN))    return 65;
            if (sinN.StartsWith(query))  return 60;
        }

        return 0;
    }
}
