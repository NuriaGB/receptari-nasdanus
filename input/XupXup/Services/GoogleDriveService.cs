using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using Microsoft.JSInterop;
using RecetariBlazor.Models;

namespace RecetariBlazor.Services;

/// <summary>
/// Gestiona l'autenticació OAuth2 amb Google i la lectura/escriptura
/// del fitxer recetari_data.json al Google Drive de l'usuari.
/// </summary>
public class GoogleDriveService
{
    private readonly HttpClient  _http;
    private readonly IJSRuntime  _js;
    private readonly IConfiguration _config;

    private string? _accessToken;
    private string? _driveFileId;   // ID del fitxer a Drive un cop trobat/creat
    private const string FileName = "recetari_data.json";

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    // Esdeveniment per notificar canvis d'estat d'autenticació
    public event Action? AuthStateChanged;

    public GoogleDriveService(HttpClient http, IJSRuntime js, IConfiguration config)
    {
        _http   = http;
        _js     = js;
        _config = config;
    }

    // ═══════════════════════════════════════════════════════════════
    //  AUTENTICACIÓ OAUTH2 (Google Identity Services)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Obre el popup d'autorització de Google.
    /// Crida a la funció JS googleSignIn() definida a google-auth.js
    /// </summary>
    public async Task SignInAsync()
    {
        var clientId = _config["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId no configurat a appsettings.json");

        // Crida JS que obre el popup OAuth i retorna l'access token
        _accessToken = await _js.InvokeAsync<string>("googleAuth.signIn", clientId);
        AuthStateChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        _accessToken = null;
        _driveFileId = null;
        await _js.InvokeVoidAsync("googleAuth.signOut");
        AuthStateChanged?.Invoke();
    }

    /// <summary>Comprova si hi ha una sessió activa guardada al localStorage.</summary>
    public async Task TryRestoreSessionAsync()
    {
        try
        {
            _accessToken = await _js.InvokeAsync<string?>("googleAuth.getStoredToken");
            if (!string.IsNullOrEmpty(_accessToken))
                AuthStateChanged?.Invoke();
        }
        catch { /* primera visita, sense token */ }
    }

    // ═══════════════════════════════════════════════════════════════
    //  LECTURA I ESCRIPTURA DEL FITXER AL DRIVE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Carrega les dades de l'app des del Drive. Crea el fitxer si no existeix.</summary>
    public async Task<AppData> LoadDataAsync()
    {
        EnsureAuthenticated();

        // 1. Cercar el fitxer
        _driveFileId = await FindFileIdAsync();

        if (_driveFileId == null)
        {
            // Primera vegada: crear fitxer buit amb categories per defecte
            var defaultData = CreateDefaultData();
            await SaveDataAsync(defaultData);
            return defaultData;
        }

        // 2. Descarregar contingut
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{_driveFileId}?alt=media");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? CreateDefaultData();

        // Rehidratar referències Recipe als MenuSlots
        RehydrateMenus(data);

        return data;
    }

    /// <summary>Desa les dades al fitxer JSON al Drive de l'usuari.</summary>
    public async Task SaveDataAsync(AppData data)
    {
        EnsureAuthenticated();
        data.LastSaved = DateTime.Now;

        var json    = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        if (_driveFileId == null)
        {
            // Crear fitxer nou (multipart: metadata + contingut)
            response = await CreateFileAsync(json);
            var created = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync());
            _driveFileId = created.GetProperty("id").GetString();
        }
        else
        {
            // Actualitzar fitxer existent (PATCH)
            var req = new HttpRequestMessage(
                HttpMethod.Patch,
                $"https://www.googleapis.com/upload/drive/v3/files/{_driveFileId}?uploadType=media");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            req.Content = content;
            response = await _http.SendAsync(req);
        }

        response.EnsureSuccessStatusCode();
    }

    // ─── Helpers privats ──────────────────────────────────────────

    private async Task<string?> FindFileIdAsync()
    {
        var query   = Uri.EscapeDataString($"name='{FileName}' and trashed=false");
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files?q={query}&fields=files(id,name)");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body  = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync());
        var files = body.GetProperty("files");

        return files.GetArrayLength() > 0
            ? files[0].GetProperty("id").GetString()
            : null;
    }

    private async Task<HttpResponseMessage> CreateFileAsync(string jsonContent)
    {
        var metadata = JsonSerializer.Serialize(new { name = FileName, mimeType = "application/json" });

        var multipart = new MultipartContent("related");

        var metaPart = new StringContent(metadata, Encoding.UTF8, "application/json");
        multipart.Add(metaPart);

        var dataPart = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        multipart.Add(dataPart);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = multipart;

        return await _http.SendAsync(request);
    }

    private static void RehydrateMenus(AppData data)
    {
        // Migration: old single RecipeId → RecipeIds
        foreach (var slot in data.Menus.SelectMany(m => m.Days).SelectMany(d => d.Slots))
        {
            if (slot.RecipeId != null && !slot.RecipeIds.Contains(slot.RecipeId))
                slot.RecipeIds.Add(slot.RecipeId);
            slot.RecipeId = null;
        }
        var recipeMap = data.Recipes.ToDictionary(r => r.Id);
        foreach (var slot in data.Menus.SelectMany(m => m.Days).SelectMany(d => d.Slots))
            slot.Recipes = slot.RecipeIds
                .Select(id => recipeMap.TryGetValue(id, out var r) ? r : null)
                .Where(r => r != null).Cast<Recipe>().ToList();
    }

    private static AppData CreateDefaultData() => new()
    {
        Categories = new List<Category>
        {
            new() { Name = "Esmorzar",     ColorHex = "#FFD700", Emoji = "🌅" },
            new() { Name = "Dinar",        ColorHex = "#FF8C00", Emoji = "☀️" },
            new() { Name = "Sopar",        ColorHex = "#4169E1", Emoji = "🌙" },
            new() { Name = "Postres",      ColorHex = "#FF69B4", Emoji = "🍰" },
            new() { Name = "Snack",        ColorHex = "#32CD32", Emoji = "🥨" },
            new() { Name = "Vegetariana",  ColorHex = "#228B22", Emoji = "🥦" },
            new() { Name = "Vegana",       ColorHex = "#006400", Emoji = "🌱" },
            new() { Name = "Sense gluten", ColorHex = "#DAA520", Emoji = "🌾" },
            new() { Name = "Ràpida",       ColorHex = "#FF4500", Emoji = "⚡" },
            new() { Name = "Italiana",     ColorHex = "#DC143C", Emoji = "🍝" },
            new() { Name = "Espanyola",    ColorHex = "#FFA500", Emoji = "🥘" },
            new() { Name = "Asiàtica",     ColorHex = "#8B0000", Emoji = "🍜" },
        }
    };

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("L'usuari no està autenticat amb Google.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented    = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
