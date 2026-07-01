using Nasdanus.KnowledgeImporter.Domain;

namespace Nasdanus.KnowledgeImporter.Providers;

public abstract class EmptyKnowledgeProvider : IKnowledgeProvider
{
    public abstract string ProviderId { get; }
    public abstract string ProviderName { get; }

    public Task<IReadOnlyList<ProviderIngredientSearchResult>> SearchIngredientsAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProviderIngredientSearchResult>>([]);

    public Task<ProviderIngredient?> DownloadIngredientAsync(
        string providerIngredientId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProviderIngredient?>(null);

    public Task<ProviderExportResult> ExportAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProviderExportResult
        {
            Provider = ProviderId,
            Ingredients = [],
            Products = []
        });
}
