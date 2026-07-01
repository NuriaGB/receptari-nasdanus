using Nasdanus.KnowledgeImporter.Domain;

namespace Nasdanus.KnowledgeImporter.Providers;

public interface IKnowledgeProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<IReadOnlyList<ProviderIngredientSearchResult>> SearchIngredientsAsync(string query, CancellationToken cancellationToken = default);
    Task<ProviderIngredient?> DownloadIngredientAsync(string providerIngredientId, CancellationToken cancellationToken = default);
    Task<ProviderExportResult> ExportAsync(CancellationToken cancellationToken = default);
}
