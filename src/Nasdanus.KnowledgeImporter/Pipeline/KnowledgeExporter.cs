using System.Text.Json;
using System.Text.Json.Serialization;
using Nasdanus.KnowledgeImporter.Domain;

namespace Nasdanus.KnowledgeImporter.Pipeline;

public sealed class KnowledgeExporter
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task ExportAsync(
        KnowledgeCatalog catalog,
        KnowledgeValidationReport validationReport,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        await WriteJsonAsync(Path.Combine(outputDirectory, "ingredients.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            catalog.Generator,
            Items = catalog.Ingredients
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "nutrition.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.Ingredients
                .Where(ingredient => ingredient.Nutrition is not null)
                .Select(ingredient => new
                {
                    IngredientId = ingredient.Id,
                    ingredient.Nutrition,
                    ingredient.Source,
                    ingredient.SourceId,
                    ingredient.LastUpdated
                })
                .ToList()
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "food-groups.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.FoodGroups
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "units.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.Units
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "seasonality.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.Seasonality
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "aliases.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.Ingredients
                .SelectMany(ingredient => ingredient.Aliases.Select(alias => new
                {
                    IngredientId = ingredient.Id,
                    Alias = alias
                }))
                .OrderBy(item => item.Alias)
                .ToList()
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "products.json"), new
        {
            catalog.SchemaVersion,
            catalog.GeneratedAt,
            Items = catalog.Products
        }, cancellationToken);

        await WriteJsonAsync(Path.Combine(outputDirectory, "validation-report.json"), validationReport, cancellationToken);
    }

    private async Task WriteJsonAsync(string path, object value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
    }
}
