# Receptari dels Nasdanus - Migration Plan

Generated: 2026-06-26

Implementation update: 2026-07-29

This plan is still useful as historical migration guidance, but several runtime decisions have already been implemented in the Blazor WebAssembly app. Current implementation details are tracked in `docs/implementation_status.md`.

Implemented runtime status:

- Static Blazor WebAssembly app with `localStorage` persistence, not SQLite.
- Local ingredient knowledge files are consumed by the app.
- Recipe ingredients can be linked to known ingredients.
- Manual nutrition can be entered per known ingredient and survives knowledge refresh.
- Planner nutrition warnings are now traceable through `/nutrition-gaps`.
- Feedback/Product Backlog is local app data and is included in JSON backups.

## Goal

Move recipe knowledge and reusable XupXup data into a clean local-first model without prematurely building UI or locking the product into the old app architecture.

## Migration rule

Do not import directly into canonical production tables. Import into staging, review, then promote.

## Phase 0 - Preserve sources

Actions:

- Keep `input/_receptes Núria.docx` unchanged.
- Keep `input/XupXup` unchanged as a historical source.
- Record source checksums before any destructive migration.
- Preserve `input/recetari_data.json`, now available as the saved XupXup export.
- Do not reuse or publish the local `client_secret_*.json` file from XupXup.

Current status:

- DOCX is available and extracted.
- XupXup static ingredient catalogue is available.
- XupXup saved recipe/menu JSON is available at `input/recetari_data.json`.

## Phase 1 - Load migration artifacts

Artifacts already generated:

- `output/extracted_recipes.json`
- `output/xupxup_saved_recipes.json`
- `output/ingredient_candidates.json`
- `output/duplicate_candidates.json`
- `output/reuse_xupxup_report.md`
- `output/recipe_family_candidates.json`
- `output/reusable_kitchen_objects.json`

Use these as import staging input, not as final schema.

Important update:

- `input/recetari_data.json` contains 73 XupXup saved records: 21 complete recipes and 52 meal ideas/placeholders.
- Import complete records through recipe staging.
- Import placeholder records through meal idea/template staging, not canonical `Recipes`.

## Phase 2 - Create staging database

Create these staging tables before canonical import:

- `ImportSources`
- `RecipeImportCandidates`
- `IngredientImportCandidates`
- `DuplicateReviewCandidates`
- `MigrationDecisions`

Reason:

- The DOCX has good recipe names and steps but inferred ingredients.
- XupXup saved data is available and should be staged separately from DOCX extraction.
- Manual review decisions should be tracked, not hidden in one-off scripts.

## Phase 3 - Seed ingredients

Seed from XupXup:

- Import 12 ingredient categories.
- Import 157 ingredient records.
- Import synonyms as aliases.
- Merge or flag duplicate `coriandre`.

Then add DOCX unmatched candidates for review:

- `massa mare`
- `llagostins`
- `moixernons`
- `formatge crema`
- `mascarpone`
- `verdinas`
- `pa de pita`
- `salsa perrins`
- `surimi`
- `moniato`
- `gremolada`
- and other terms in `output/ingredient_candidates.json`.

Recommended treatment:

- Ingredients that can be bought -> `Ingredients`.
- Reusable preparations -> `BasePreparations`.
- Subtypes such as flour strength or pasta shape -> aliases first, child ingredients later if shopping precision requires it.

Also import from saved XupXup data:

- 101 structured ingredient-name candidates from saved recipe ingredients.
- 2 custom ingredients: `gingebre fresc` and `gingebre sec`.
- Treat XupXup recipe ingredients as higher-confidence than DOCX prose candidates, but still review units and canonical names.

## Phase 4 - Import DOCX recipes to staging

For each DOCX recipe:

- `Heading 2` -> `raw_name`.
- `Heading 1` -> `raw_category`.
- Normal paragraphs -> description/notes.
- List paragraphs -> raw steps.
- Ingredient matches -> `raw_ingredient_candidates_json`.
- Source paragraph range -> provenance.
- Review priority -> staging review queue.

Do not create canonical `RecipeIngredients` automatically. Create suggested mappings only.

High-priority manual review queue:

- Endívies al forn amb tomàquets i espècies.
- Quiche de Pastanaga.
- Spanakopita (pasta fullada grega amb espinacs).
- Fricandó amb moixernons.
- Vedella rostida.
- Mahonesa / Lactonesa.
- Coca de Sant Joan (versió millorada), both extracted copies.

## Phase 4b - Import XupXup saved records to staging

For complete XupXup recipes:

- Preserve `xupxup_id`.
- Import structured ingredients with raw quantity/unit/name.
- Import steps and timer fields.
- Import categories as source tags/roles.
- Mark prep/cook time as review-needed when both are zero.

For XupXup placeholders:

- Import as `MealIdeas` or `RecipeImportCandidates` with status `draft`.
- Do not promote to canonical recipe without ingredients/steps.
- Use them to discover Template Recipes and menu patterns.

Complete XupXup recipes: 21.
Meal ideas/placeholders: 52.

## Phase 5 - Extract reusable kitchen objects

Promote these only after review:

- Base preparations: beixamel, salsa de tomàquet, massa mare, preferments, sofregit, fumet, brou, marinades, crema components, emulsified sauces.
- Cooking techniques: oven, gratin, air fryer, wok, pan saute, cassola/guisat, boiling, steaming, frying, roasting, marinating, blending, emulsifying, fermentation, bain-marie.
- Kitchen rules: soaking, desalting, fermentation lead time, marinade lead time, bread-serving suggestions, leftover transformations, seasonal feast timing.

Reason:

- These objects reduce duplicated maintenance.
- They make meal planning more useful than a static recipe list.
- They support future import because imported text can match known techniques/templates.

## Phase 6 - Resolve families, templates, duplicates, and adaptations

Decisions to make:

- Merge the duplicate `Coca de Sant Joan (versió millorada)` copies.
- Decide whether improved `Coca de Sant Joan` replaces, adapts, or coexists with the original.
- Keep `Canelons de carbassó i llagostins` and `Canelons de carbassó amb formatge` as variants unless review says otherwise.
- Model `Massa Mare Iban Yarza` as `BasePreparation`.
- Decide whether `Magdalenes Dukan` is a standalone recipe or `RecipeAdaptation`.
- Create `TemplateRecipe` records where the pattern is stable: canelons de carbassó, filet de porc, salses emulsionades, fermented doughs, cold soups, rotllets de primavera, iogurt bowls, amanides ràpides, truita francesa, samuses, and llenties amb verduretes.
- Keep hero-ingredient families such as bacallà and pollastre separate from duplicate detection.

Record each decision in `MigrationDecisions`.

## Phase 7 - XupXup `recetari_data.json` mapping

Migration viability:

- The saved XupXup JSON is now available at `input/recetari_data.json`.
- It is migrable, but split complete recipes from placeholders.

Mapping:

| XupXup | Target |
|---|---|
| `AppData.Recipes` | `RecipeImportCandidates`, then `Recipes` after review |
| `Recipe.CategoryIds` | Recipe tags/source categories |
| `Recipe.Ingredients` | `RecipeIngredients` with raw quantity/unit preserved |
| `Recipe.Equipment` | Recipe notes or future equipment table |
| `Recipe.Steps` | `RecipeSteps` |
| `RecipeStep.TimerMinutes` | `RecipeSteps.timer_minutes` |
| `RecipeStep.Links` | Ingredient-step annotations, optional future feature |
| `AppData.Categories` | Recipe tags/categories |
| `AppData.Menus` | `MealPlan` and `MealPlanSlots` |
| `CustomIngredients` | `Ingredients` / `IngredientAliases` review queue |
| XupXup records with no ingredients/steps | `MealIdeas` or draft `RecipeImportCandidates` |

Fields to avoid carrying over blindly:

- `ImageBase64`: export images to files/blob storage if needed.
- `DescriptionHtml`: sanitize and preserve as raw import text only.
- Google Drive file ID/token state: do not migrate.

## Phase 8 - Promote reviewed recipes

Promotion checklist:

- Name confirmed.
- Category/status confirmed.
- Duplicate/adaptation decision recorded.
- Ingredients reviewed or deliberately left raw.
- At least primary balance roles assigned.
- Seasonal tags added where obvious.
- Freezer suitability marked if known.
- Approx kcal band left blank unless there is enough confidence.

Canonical promotion can be incremental. The app can be useful with a partially reviewed recipe set as long as review status is visible.

## Phase 9 - Add ERP data gradually

After recipes are staged:

1. Seed pantry staples.
2. Add low-stock thresholds.
3. Add freezer lots.
4. Add weekly balance default rules.
5. Create one sample meal plan from real family usage.
6. Generate shopping list and thawing plan from that sample.

This validates the target model before UI investment.

## Risks

- Ingredient inference may over-match common words such as `pa`, `sal`, `oli`, or `all`.
- Quantities are mostly absent from the DOCX.
- Some DOCX recipes include copied descriptions or stray steps.
- XupXup user data may exist only in Google Drive.
- Old XupXup model does not cover pantry/freezer/shopping/thawing.

## Validation checks

Minimum checks before building UI:

- Every extracted recipe has a source ID and paragraph range.
- Duplicate queue has been reviewed.
- Ingredient catalogue has no duplicate canonical names unless intentionally modeled.
- Every canonical recipe has at least one `RecipeRole`.
- FEFO query returns freezer lots in correct order.
- Low-stock rule creates one merged shopping item, not duplicates.
- Meal plan balance warnings are explainable from `RecipeRoles`.
- Thawing plan can be generated from a freezer-backed meal slot.
