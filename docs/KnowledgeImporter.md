# Nasdanus Knowledge Importer

`Nasdanus.KnowledgeImporter` is a developer tool for building local Nasdanus knowledge files.

It is not part of the Blazor application and must not be shipped as user-facing functionality.

## Purpose

External providers are only sources used to build Nasdanus-owned JSON files.

The application should consume local knowledge files later, never external nutrition APIs directly.

Pipeline:

```text
External Provider
-> Importer
-> Normalisation
-> Validation
-> Knowledge/*.json
-> Nasdanus Application
```

## Run

From the repository root:

```powershell
dotnet run --project src\Nasdanus.KnowledgeImporter\Nasdanus.KnowledgeImporter.csproj -- --output Knowledge
```

Generated files:

- `Knowledge/ingredients.json`
- `Knowledge/nutrition.json`
- `Knowledge/food-groups.json`
- `Knowledge/units.json`
- `Knowledge/seasonality.json`
- `Knowledge/aliases.json`
- `Knowledge/products.json`
- `Knowledge/validation-report.json`

## Providers

The provider interface is `IKnowledgeProvider`.

Initial providers are intentionally empty:

- `BedcaProvider`
- `CiqualProvider`
- `OpenFoodFactsProvider`

Future implementations should download or import external data, then export provider records into the canonical model.

## Domain Rules

- Recipes reference Ingredients.
- Recipes never reference commercial Products.
- Products may later reference Ingredients.
- Nutrition belongs to Ingredients.
- Provider-specific models must not leak into the Blazor application.

## Validation

The importer reports:

- Unknown categories.
- Duplicate aliases.
- Missing nutrition.
- Missing units.
- Duplicate ingredients.
