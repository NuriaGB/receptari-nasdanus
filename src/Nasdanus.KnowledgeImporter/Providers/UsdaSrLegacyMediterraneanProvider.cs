using System.Text.Json;
using System.Text.Json.Serialization;
using Nasdanus.KnowledgeImporter.Domain;
using Nasdanus.KnowledgeImporter.Pipeline;

namespace Nasdanus.KnowledgeImporter.Providers;

public sealed class UsdaSrLegacyMediterraneanProvider : IKnowledgeProvider
{
    private const string SourceFile = "Data/usda-sr-legacy-mediterranean-starter.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private ProviderSeedFile? seed;

    public string ProviderId => "usda-sr-legacy";
    public string ProviderName => "USDA FoodData Central SR Legacy";

    public async Task<IReadOnlyList<ProviderIngredientSearchResult>> SearchIngredientsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = KnowledgeNormalizer.NormalizeKey(query);
        var items = await LoadItemsAsync(cancellationToken);
        return items
            .Where(ingredient => string.IsNullOrWhiteSpace(normalizedQuery)
                || KnowledgeNormalizer.NormalizeKey(ingredient.Name).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || KnowledgeNormalizer.NormalizeKey(ingredient.CatalanName).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || KnowledgeNormalizer.NormalizeKey(ingredient.SpanishName).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || ingredient.Aliases.Any(alias => KnowledgeNormalizer.NormalizeKey(alias).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(ingredient => new ProviderIngredientSearchResult
            {
                Provider = ProviderId,
                ProviderId = ingredient.ProviderId,
                Name = ingredient.Name
            })
            .ToList();
    }

    public async Task<ProviderIngredient?> DownloadIngredientAsync(
        string providerIngredientId,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadItemsAsync(cancellationToken);
        return items.FirstOrDefault(ingredient =>
            string.Equals(ingredient.ProviderId, providerIngredientId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProviderExportResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        var items = await LoadItemsAsync(cancellationToken);
        return new ProviderExportResult
        {
            Provider = ProviderId,
            Ingredients = items
        };
    }

    private async Task<IReadOnlyList<ProviderIngredient>> LoadItemsAsync(CancellationToken cancellationToken)
    {
        if (seed is not null)
        {
            return seed.Items;
        }

        var path = Path.Combine(AppContext.BaseDirectory, SourceFile);
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "src", "Nasdanus.KnowledgeImporter", SourceFile);
        }

        await using var stream = File.OpenRead(path);
        seed = await JsonSerializer.DeserializeAsync<ProviderSeedFile>(stream, JsonOptions, cancellationToken)
            ?? new ProviderSeedFile();

        foreach (var item in seed.Items)
        {
            item.Provider = ProviderId;
            item.LastUpdated = DateTimeOffset.TryParse(seed.GeneratedAt, out var generatedAt)
                ? generatedAt
                : DateTimeOffset.UtcNow;
        }

        return seed.Items;
    }

    private sealed class ProviderSeedFile
    {
        public string GeneratedAt { get; set; } = string.Empty;
        public List<ProviderIngredient> Items { get; set; } = [];
    }
}
