using Nasdanus.KnowledgeImporter.Domain;
using Nasdanus.KnowledgeImporter.Providers;

namespace Nasdanus.KnowledgeImporter.Pipeline;

public sealed class KnowledgeImportPipeline(
    IReadOnlyList<IKnowledgeProvider> providers,
    KnowledgeNormalizer normalizer,
    KnowledgeValidator validator,
    KnowledgeExporter exporter)
{
    public async Task<KnowledgePipelineResult> RunAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        var providerExports = new List<ProviderExportResult>();
        foreach (var provider in providers)
        {
            providerExports.Add(await provider.ExportAsync(cancellationToken));
        }

        var catalog = normalizer.Normalize(providerExports);
        var validationReport = validator.Validate(catalog);
        await exporter.ExportAsync(catalog, validationReport, outputDirectory, cancellationToken);

        return new KnowledgePipelineResult(catalog, validationReport, outputDirectory);
    }
}

public sealed record KnowledgePipelineResult(
    KnowledgeCatalog Catalog,
    KnowledgeValidationReport ValidationReport,
    string OutputDirectory);
