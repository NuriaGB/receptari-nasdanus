# Receptari dels Nasdanus - Architecture and Migration Analysis

Generated: 2026-06-26

Implementation update: 2026-07-29

The original recommendation was domain-first and assumed a future local SQLite runtime. The current implemented app is a static Blazor WebAssembly application using browser `localStorage`, with local JSON knowledge files and export/import backups. For the current runtime state and code map, see `docs/implementation_status.md`.

Important implemented deltas since this analysis:

- Ingredient Knowledge is now active in the app: recipes can link ingredients to known ingredients.
- Nutrition is calculated from linked ingredients and usable quantities.
- Manual nutrition entry exists for known ingredients and is preserved as source `manual`.
- Planner daily totals are shown per serving/person.
- Daily goal checks compare protein against 80%-110% of the configured target and kcal against the configured daily limit.
- `/nutrition-gaps` makes planner nutrition warnings actionable by separating planned issues, missing nutrition, unresolved ingredients and unusable quantities.
- Feedback is stored as `ProductBacklogItems` in the local app state and can be reviewed at `/backlog`.

## Executive summary

The Word recipe book is the primary source of family recipe knowledge. It contains 62 extracted recipe records, organized by Word headings into six source categories: Entrants i primers, Segons, Salses, Postres, Pa, and Receptes a provar. Recipe names and preparation steps are reliable enough to migrate into an import staging area. Ingredients are not reliable enough for direct structured import because the DOCX does not contain explicit ingredient sections; ingredient candidates were inferred from titles, descriptions, and steps.

The XupXup app should not be preserved as the new app. It is useful as evidence: it has a valuable ingredient catalogue with aliases, a simple recipe/menu JSON shape, a scaling service, saved menus, saved recipes, and lessons about what felt tedious. The saved app export is now available at `input/recetari_data.json`: it contains 73 recipe records, including 21 complete structured recipes and 52 meal ideas/placeholders.

Recommended direction: build Receptari dels Nasdanus around a local-first normalized SQLite domain model, with a separate import/review pipeline. Treat the DOCX extraction and XupXup JSON export as migration inputs, not as the production schema.

## Source inventory

### `_receptes Núria.docx`

Extraction method:

- Word `Heading 1` -> source recipe category.
- Word `Heading 2` -> recipe name.
- Word `List Paragraph` -> preparation steps.
- Word `Normal` paragraphs -> descriptions, tips, notes, or section labels.
- XupXup `ingredients-db.json` plus a small unmatched-term lexicon -> ingredient candidates.

Observed structure:

- Non-empty paragraphs: 858.
- Tables: 0.
- Recipe records extracted before the alphabetical index: 62.
- No dedicated ingredient tables or consistent ingredient sections.

Category counts:

| Source category | Count |
|---|---:|
| Entrants i primers | 18 |
| Segons | 14 |
| Salses | 2 |
| Postres | 13 |
| Pa | 9 |
| Receptes a provar | 6 |

Generated artifact: `output/extracted_recipes.json`.

### XupXup

XupXup is a Blazor WebAssembly app:

- .NET 8 / Blazor WebAssembly.
- MudBlazor UI.
- Google Drive API for storage.
- Static ingredient catalogue in `wwwroot/data/ingredients-db.json`.
- App data expected in a Google Drive file named `recetari_data.json`.

Local folder contents include code, static catalogue data, OAuth configuration, and build artifacts. A saved app export is now available separately at `input/recetari_data.json`.

Saved XupXup data summary:

- 73 saved recipe records.
- 21 complete recipes with structured ingredients and steps.
- 52 meal ideas/placeholders without ingredients or steps.
- 12 recipe categories.
- 3 menus.
- 2 custom ingredients.
- Last saved: `2026-05-10T13:46:44.834+02:00`.

Generated artifact: `output/reuse_xupxup_report.md`.
Saved-data artifact: `output/xupxup_saved_recipes.json`.

## DOCX extraction findings

### Recipe names

Recipe names were extracted reliably from `Heading 2`. The only exact duplicate title is:

- `Coca de Sant Joan (versió millorada)` appears twice with identical first-step content.

High-priority manual review recipes:

| Recipe | Reason |
|---|---|
| Endívies al forn amb tomàquets i espècies | Orphan punctuation paragraph in source. |
| Quiche de Pastanaga | Contains two salmon-cooking steps that appear unrelated to the quiche. |
| Spanakopita (pasta fullada grega amb espinacs) | Description appears copied from Sopa de Ceba. |
| Fricandó amb moixernons * | Title contains an asterisk marker. |
| Vedella rostida | Orphan punctuation paragraph in source. |
| Mahonesa / Lactonesa | Orphan single-letter paragraph and few ingredient candidates. |
| Coca de Sant Joan (versió millorada), copy 1 | Duplicate title, trial category. |
| Coca de Sant Joan (versió millorada), copy 2 | Duplicate title, trial category. |

Medium-priority review is also needed for all `Receptes a provar`, the external-source recipe `Brotxetes de pollastre amb pinya picant`, and terse recipes such as `Nuggets de pollastre`, `Salsa BURGER`, `Doriyakis`, `Magdalenes Dukan`, and `Massa Mare Iban Yarza`.

Important: every recipe still needs ingredient/quantity review before production import, because ingredients were inferred from prose.

### Ingredients

Ingredient candidate extraction found:

- 157 XupXup catalogue ingredients across 12 categories.
- 90 XupXup catalogue ingredients matched somewhere in the DOCX.
- 39 unmatched DOCX ingredient/base-preparation terms worth adding or mapping.
- One duplicate catalogue name: `coriandre`.

Most common matched candidates:

| Candidate | Category | Matched recipe count |
|---|---|---:|
| sal | condiments | 38 |
| oli d'oliva | oli_salses | 31 |
| farina | cereals | 26 |
| mantega | ous_lactics | 21 |
| all | verdura | 21 |
| llet | ous_lactics | 19 |
| pa | cereals | 18 |
| ou | ous_lactics | 18 |
| sucre | condiments | 17 |
| ceba | verdura | 17 |
| tomàquet | verdura | 16 |

Useful unmatched terms include `massa mare`, `llagostins`, `moixernons`, `formatge crema`, `mascarpone`, `salsa perrins`, `verdinas`, `pa de pita`, `surimi`, `moniato`, and `gremolada`.

Generated artifact: `output/ingredient_candidates.json`.

### Preparation steps

Preparation steps are usually recoverable because the DOCX uses list paragraphs. Some recipes contain descriptive prose, source notes, tips, and section headings mixed into the recipe body. These should be imported into staging as-is and reviewed before conversion into normalized steps, ingredients, base preparations, or notes.

## XupXup findings

### Existing data model

XupXup has these persisted concepts:

- `AppData`: recipes, categories, menus, custom ingredients, last saved timestamp, version.
- `Recipe`: name, description, prep/cook time, servings, difficulty, base64 image, tips, category IDs, ingredients, equipment, steps.
- `Ingredient`: name, quantity, unit, order, ingredient category ID.
- `RecipeStep`: plain text, rich HTML, optional timer, optional image, step ingredient links.
- `MenuPlan`: name, start/end date, days.
- `MenuDay`: date and slots.
- `MenuSlot`: meal slot type and recipe IDs.
- `CustomIngredientEntry`: user-added ingredient aliases/categories.
- Static `IngredientDatabase`: categories and ingredients with synonyms.

The newly available `input/recetari_data.json` confirms that the model is not just theoretical. It contains real saved data:

- Complete structured recipes such as `Cookies d'Oreo i formatge crema`, `Samuses de vedella amb verduretes a l'airfryer`, `Bunyols de vent`, `Bunyols de l'Empordà`, `Llenties estofades amb verduretes i arròs`, and `Croquetes de Pollastre rostit`.
- Meal ideas such as `Rotllets de primavera amb pavo`, `Amanida verda`, `Truita francesa amb crema de carbassó`, and several yogurt/fruit snack combinations.
- Three saved menu plans, including `Menú antiinflamatori`.
- Two custom ginger ingredients: `gingebre fresc` and `gingebre sec`.

### Reusable parts

Reuse strongly:

- `ingredients-db.json` as seed data for `Ingredients` and `IngredientAliases`.
- Ingredient category taxonomy as a first-pass weekly-balance taxonomy.
- Alias matching ideas from `IngredientDbService`.

Reuse selectively:

- `IngredientScalingService` logic for scaling warnings, but redesign around normalized units and raw text preservation.
- Menu-plan shape as inspiration, but not as final schema.
- Step timers and step-by-step cooking concept as future UX/domain fields.

Do not reuse as foundation:

- Google Drive as the primary local-first store.
- One big `AppData` JSON file as the production database.
- Blazor page/forms as the product workflow.
- Base64 image storage inside recipe records.
- Manual rich-text ingredient linking as the main import mechanism.

## Why XupXup likely became tedious

XupXup asks the user to structure too much too early: recipe name, categories, ingredients, quantities, units, equipment, steps, rich text, and optional ingredient-step links. That is useful for a clean database but slow for family recipe capture.

Other friction points:

- No easy import flow from DOCX, copied text, websites, photos, or notes.
- Ingredient linking requires selecting text and connecting it manually.
- Menu planning is not connected to pantry, freezer, FEFO, thawing, shopping, or weekly nutrition balance.
- Google OAuth/Drive setup creates operational friction.
- A single JSON file is simple but fragile for conflict handling, partial sync, audit, and Android portability.
- The model tracks recipes but not the kitchen ERP concepts the new product needs: pantry stock, freezer lots, low-stock thresholds, thawing plan, meal history, balance rules, and approximate kcal warnings.

## Comparison between sources

### Duplicates and similar recipes

Generated artifact: `output/duplicate_candidates.json`.
Additional kitchen-knowledge artifacts:

- `output/recipe_family_candidates.json`
- `output/reusable_kitchen_objects.json`

Key candidates:

| Type | Recipes | Recommendation |
|---|---|---|
| Exact duplicate | `Coca de Sant Joan (versió millorada)` x2 | Merge into one draft. |
| Version/adaptation | `Coca de Sant Joan` and improved version | Keep original plus improved adaptation or replace after family review. |
| Variant | `Canelons de carbassó i llagostins` and `Canelons de carbassó amb formatge` | Keep as variants under a recipe family. |
| Same primary ingredient | `Filet de porc amb salsa al pebre verd` and `Filet de porc al forn` | Keep separate recipes. |
| Adaptation | `Magdalenes` and `Magdalenes Dukan` | Consider `RecipeAdaptation`. |
| Base preparation dependency | `Massa Mare Iban Yarza` and `Pa de Massa Mare Iban Yarza` | Model `Massa Mare` as `BasePreparation`, not duplicate recipe. |
| Cross-source exact title | DOCX `Bunyols de vent` and XupXup `Bunyols de vent` | Likely same recipe; compare XupXup structured ingredients/steps with DOCX text before merging. |
| Cross-source exact title | DOCX `Pollastre Rostit` and XupXup `Pollastre rostit` | Same title, but XupXup entry is a placeholder; likely link as meal idea or leftover family member. |
| Cross-source similar title | DOCX `Bunyols de l'Empordà` and XupXup `Bunyols de l'Empordà` | Likely same recipe with punctuation/accent variation. |
| Cross-source family | DOCX `Arròs tres delícies` and XupXup `Salmó a la papillote amb arròs tres delícies` | Not a duplicate; `Arròs tres delícies` may be a base/side component. |
| XupXup internal similar | `Amanida d'esqueixada de Bacallà` and `Amanida amb esqueixada de bacallà` | Likely duplicate meal ideas or one salad template variant. |
| XupXup internal family | Multiple `Iogurt amb...` entries | Better as a snack template, not repeated recipes. |

Updated duplicate candidate counts:

- 19 total candidate pairs.
- 7 DOCX-internal pairs.
- 6 cross-source DOCX/XupXup pairs.
- 6 XupXup-internal pairs.

### Missing fields

Missing or inconsistent in the DOCX:

- Structured ingredient quantities.
- Normalized units.
- Servings/yield for most recipes.
- Prep time and cook time as structured fields.
- Freezer suitability.
- Pantry staples vs shopping-needed ingredients.
- Seasonality as structured metadata.
- Dietary flags and child/family adaptations.
- kcal estimates.
- Weekly balance roles.
- Last cooked date / meal history.
- Source provenance beyond occasional notes and one URL.

Missing in XupXup relative to product goals:

- Pantry stock.
- Freezer lots and FEFO.
- Thawing planner.
- Shopping list tied to stock and meal plan.
- Meal history.
- Balance rules.
- Approximate kcal warnings.
- Seasonal availability and seasonal recipe surfacing.
- Import staging/review.

Additional XupXup saved-data finding:

- 21 records have structured ingredients and steps and are good migration candidates.
- 52 records are meal ideas/placeholders. These are still valuable for menu planning, family preferences, and Template Recipes, but should not be promoted as complete recipes until reviewed.
- XupXup has structured quantities, but units and ingredient names still need canonical cleanup. Examples include custom names such as `Farina blat (tot ús)`, `Sucre blanc`, `Panela`, and duplicate ginger aliases.

### Inconsistent ingredient names

The main inconsistencies to normalize early:

- `oli`, `AOVE`, `aceite`, `oli d'oliva`.
- `pebre vermell`, `pimentón`, `paprika`, `pebre dolç`.
- `pasta`, `fideus`, `macarrons`, `espaguetis`.
- `farina`, `farina de força`, `farina integral`, `farina de blat tendre`.
- `cayena`, `caiena`, `xili`, `bitxo`, `guindilla`.
- `coriandre` duplicated in XupXup catalogue.
- Ingredient vs base preparation: `massa mare`, `gremolada`, `crema catalana`, `nata muntada`, `massa de pizza`.
- XupXup saved custom ingredients: `gingebre fresc` and `gingebre sec` both use alias `gingebre`; keep fresh and dry as separate canonical ingredients or ingredient forms.

## Clean canonical recipe list

This list keeps the DOCX recipes, removes the exact duplicate copy of `Coca de Sant Joan (versió millorada)`, and treats `Receptes a provar` as draft status. It does not invent recipes. XupXup saved complete recipes are listed separately because they are structured app records, not part of the DOCX book.

### Entrants i primers

- Arròs tres delícies
- Canelons de carbassó i llagostins
- Cous cous Marroquí
- Endívies al forn amb tomàquets i espècies
- Espaguetis amb salsa putanesca
- Fideus amb albergínia i mango
- Gaspatxo de síndria
- Llenties a la riojana
- Macarrons de Pernil i Xoriço
- Paella de Marisc
- Pastís de salmó i formatge
- Quiche de Pastanaga
- Seques amb bacallà
- Sopa de Ceba
- Sopa de Meló
- Spanakopita (pasta fullada grega amb espinacs)
- Tzatziki (Amanida de cogombre i iogurt grec)
- Vichyssoise

### Segons

- Bacallà amb samfaina
- Crespells de Bacallà
- Brotxetes de pollastre amb pinya picant
- Filet de porc amb salsa al pebre verd
- Fricandó amb moixernons
- Gall d'indi a la mostassa
- Nuggets de pollastre
- Ossobuco
- Pollastre al curry
- Pollastre Rostit
- Sardines en escabetx
- Shawarma de xai
- Vedella rostida
- Xai al vi negre

### Salses

- Mahonesa / Lactonesa
- Salsa BURGER

### Postres

- Bunyols de vent
- Bunyols de l'Empordà
- Coca de iogurt
- Coca de Sant Joan
- Crema Catalana
- Doriyakis
- Flam d'Ou
- LemonPie
- Magdalenes
- Magdalenes Dukan
- Pa de pessic de Vic
- Panellets
- Pastís de Formatge sense forn

### Pa and base preparations

- Massa Mare Iban Yarza - recommend `BasePreparation`.
- Pa de Massa Mare Iban Yarza
- Pizza Iban Yarza
- Pa Rústic Webos
- Panets de llavors Webos
- Pa Turris
- Molletes
- Pa de Llet Alma
- Fajitas Dürum

### Draft / to review

- Coca de Sant Joan (versió millorada) - keep one copy only.
- Verdinas amb xoriço i verdures
- Filet de porc al forn
- Canelons de carbassó amb formatge
- Pa de pessic taronja i civada

### Additional XupXup complete recipes to stage

These 21 entries have structured ingredients and steps in `input/recetari_data.json`:

- Cookies d'Oreo i formatge crema
- Plumcake de taronja tendre
- Plumcake de lacasitos
- Mini foskitos
- Brownie de xocolata amb marshmallows
- Samuses de vedella amb verduretes a l'airfryer
- Blat sarraí cremós amb pit de pollastre i verduretes
- Fals rissotto amb gambes i musclos
- Cassoleta de verdures, pollastre i patata bullida
- Bowl d'arròs amb crema de pastanaga, endívies i pollastre a la cúrcuma (airfryer)
- Panets COra
- Pa de Pita integral
- Pollastre especiat amb arròs
- Bunyols de vent
- Bunyols de l'Empordà
- Pastís mona tipus Foskito
- Spaghetti carbonara a mi manera
- Verat amb pernil i patates
- Llenties estofades amb verduretes i arròs
- Llenties amb verduretes, ou poché i mozzarella
- Croquetes de Pollastre rostit

The 52 remaining XupXup records should be imported as meal ideas or draft recipe candidates, not complete recipes.

## Beyond recipes: reusable kitchen knowledge

The new system should not flatten everything into standalone recipes. The DOCX already contains reusable objects: sauces, doughs, prep rules, techniques, serving rules, seasonal knowledge, and families of variants. These should become first-class records.

### Template Recipe concept

A `TemplateRecipe` is a reusable cooking pattern with slots. Variants fill those slots.

User-provided examples:

- Template: Rotllets de primavera. Variants: pollastre, ànec, gall dindi, vedella.
- Template: Pollastre al forn. Variants: llimona, mediterrani, cervesa, espècies.

These examples are product-model examples. `Rotllets de primavera amb pavo` also appears in the XupXup saved JSON as a draft/meal idea, so the template concept is now supported by an actual source record even though that record has no structured ingredients or steps yet.

### Recipe families and templates found in the sources

| Family / template candidate | Members found | Recommendation |
|---|---:|---|
| Canelons de carbassó | 2 | Template with wrapper, filling, sauce, gratin topping. |
| Coca de Sant Joan | 3 extracted records | Merge duplicate improved copy, then model original/improved as versions. |
| Filet de porc | 2 | Template by cut + method + sauce/rub. |
| Magdalenes | 2 | Possible base recipe + diet adaptation. |
| Pa de pessic | 3 | Template/base preparation; includes sweet and savory use. |
| Pans i masses fermentades | 8 | Family with `Massa Mare` and preferments as base preparations. |
| Bunyols | 2 | Family for seasonal fried dough sweets. |
| Sopes i cremes fredes | 3 | Summer cold-soup template candidate. |
| Bacallà | 3 | Hero-ingredient family, not duplicates. |
| Pollastre | 4 | Hero-ingredient family; future oven-chicken template fits here. |
| Salses emulsionades | 2 | Template/base preparation for mahonesa, lactonesa, burger sauce. |
| Rotllets de primavera | 1 XupXup draft | Template candidate with pavo variant; incomplete recipe. |
| Iogurt amb fruita/fruits secs/xocolata | 6 XupXup meal ideas | Snack template, not six full recipes. |
| Amanides ràpides | 7 XupXup meal ideas | Salad template with base/protein/extras/dressing slots. |
| Truita francesa | 3 XupXup meal ideas | Quick dinner template. |
| Samuses | 2 XupXup records | Template with one complete vedella recipe and one pollastre draft. |
| Llenties amb verduretes | 2 XupXup complete recipes | Legume template/variant group. |

Generated artifact: `output/recipe_family_candidates.json`.

### Reusable base preparations found

Candidates include:

- Arròs cuit base.
- Truita fina.
- Beixamel.
- Salsa de tomàquet.
- Samfaina.
- Ceba caramelitzada.
- Vinagreta.
- Sofregit.
- Fumet / brou de peix.
- Brou de pollastre.
- Mousse de formatge.
- Pa de pessic.
- Massa mare.
- Preferment / ferment.
- Massa de pizza.
- Crema catalana / crema pastissera.
- Merengue suís.
- Caramel.
- Mahonesa / lactonesa.
- Gremolada.
- Marinada.

These should not all become full recipes. Some belong in `BasePreparations`, some are `CookingTechniques`, and some are template components.

### Cooking techniques found

Technique candidates:

- Forn / baked.
- Gratinar.
- Air fryer.
- Wok.
- Paella / pan saute.
- Cassola / guisat.
- Olla / bullit.
- Vaporera / steam.
- Fregir.
- Rostir.
- Marinar.
- Triturar / blender.
- Emulsionar.
- Fermentar / llevar.
- Bany maria.

These are useful for filtering, planning effort, equipment reminders, batch cooking, and future import parsing.

### Recipe roles found

Role candidates go beyond source categories:

- starter
- main
- sauce
- dessert
- bread_or_dough
- draft_to_test
- side_or_component
- one_pot
- make_ahead
- family_feast_or_seasonal

These are separate from weekly balance roles. A recipe can be both `main` and `fish`, or `side_or_component` and `vegetables`.

### Kitchen knowledge candidates

The DOCX includes reusable rules:

- Advance soak rule for legumes.
- Advance desalting rule for bacallà.
- Dough fermentation timing.
- Marinade timing.
- Serve-with-bread rule for saucy dishes.
- Child-friendly texture adaptation.
- Leftover transformation from rostit to canelons/croquetes.
- Massa mare maintenance.
- Seasonal feast knowledge: Sant Joan, Setmana Santa, Quaresma, Tots Sants, Sant Josep.

Generated artifact: `output/reusable_kitchen_objects.json`.

## Architecture recommendation

Use a layered local-first architecture:

1. Domain model and rules in a UI-independent package.
2. SQLite normalized store on device/desktop.
3. Import staging tables for DOCX, XupXup JSON, future web/photo/text imports.
4. Manual review workflow before promoting staged recipes to canonical recipes.
5. Optional sync/export later, after local correctness is solid.

The product should optimize for quick capture and later cleanup. The old XupXup flow optimized for clean entry at creation time, which is the opposite of what the DOCX migration and future recipe import need.
