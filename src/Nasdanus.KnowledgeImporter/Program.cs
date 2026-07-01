using Nasdanus.KnowledgeImporter.Pipeline;
using Nasdanus.KnowledgeImporter.Providers;

var outputDirectory = ParseOutputDirectory(args);
var providers = new IKnowledgeProvider[]
{
    new LocalSeedKnowledgeProvider(),
    new BedcaProvider(),
    new CiqualProvider(),
    new OpenFoodFactsProvider()
};

var pipeline = new KnowledgeImportPipeline(
    providers,
    new KnowledgeNormalizer(),
    new KnowledgeValidator(),
    new KnowledgeExporter());

var result = await pipeline.RunAsync(outputDirectory);

Console.WriteLine("Nasdanus knowledge import complete.");
Console.WriteLine($"Output: {Path.GetFullPath(result.OutputDirectory)}");
Console.WriteLine($"Ingredients: {result.Catalog.Ingredients.Count}");
Console.WriteLine($"Products: {result.Catalog.Products.Count}");
Console.WriteLine($"Validation issues: {(result.ValidationReport.HasIssues ? "yes" : "no")}");

if (result.ValidationReport.HasIssues)
{
    Console.WriteLine("See Knowledge/validation-report.json for details.");
}

static string ParseOutputDirectory(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (args[index] is "--output" or "-o")
        {
            return index + 1 < args.Length ? args[index + 1] : "Knowledge";
        }
    }

    return "Knowledge";
}
