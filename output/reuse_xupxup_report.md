# XupXup Reuse Report

Generated: 2026-06-26

## Verdict

Do not preserve XupXup as the foundation app. Preserve its data and lessons.

Update: `input/recetari_data.json` is now available and contains the saved XupXup app data. The earlier code-only analysis was incomplete for saved recipes because this export was not present in the XupXup folder.

The reusable value is mainly:

- Ingredient catalogue and aliases.
- 21 complete saved recipes with structured ingredients and steps.
- 52 meal ideas/placeholders useful for meal planning and Template Recipes.
- 3 saved menu plans.
- Basic recipe/menu schema as migration source.
- Ingredient matching/autocomplete ideas.
- Scaling warning logic.
- Evidence of workflows that became too manual.

The app architecture does not match the new kitchen ERP goals: pantry, freezer FEFO, thawing, shopping, weekly balance, meal history, seasonal recipes, low-stock automation, and Android portability.

It also does not model reusable kitchen knowledge as first-class data. Recipes, ingredients, and menus exist, but base preparations, recipe families, template recipes, cooking techniques, lead-time rules, and leftover rules are either implicit in prose or absent.

## Tech stack

- .NET 8.
- Blazor WebAssembly.
- MudBlazor.
- Google Drive API.
- Google Identity Services OAuth helper JavaScript.
- Static JSON catalogue in `wwwroot/data/ingredients-db.json`.
- User data expected in Google Drive as `recetari_data.json`.

Project files inspected:

- `input/XupXup/RecetariBlazor.csproj`
- `input/XupXup/Program.cs`
- `input/XupXup/Models/Models.cs`
- `input/XupXup/Services/AppState.cs`
- `input/XupXup/Services/GoogleDriveService.cs`
- `input/XupXup/Services/IngredientDbService.cs`
- `input/XupXup/Services/IngredientScalingService.cs`
- `input/XupXup/wwwroot/data/ingredients-db.json`
- `input/recetari_data.json`

## Existing data model

### Persisted root

`AppData`

- `Recipes`
- `Categories`
- `Menus`
- `CustomIngredients`
- `LastSaved`
- `Version`

### Recipe model

`Recipe`

- `Id`
- `Name`
- `Description`
- `PrepTimeMinutes`
- `CookTimeMinutes`
- `Servings`
- `Difficulty`
- `ImageBase64`
- `Tips`
- `CreatedAt`
- `UpdatedAt`
- `CategoryIds`
- `Ingredients`
- `Equipment`
- `Steps`

`Ingredient`

- `Id`
- `Name`
- `Quantity`
- `Unit`
- `Order`
- `CategoriaId`

`RecipeStep`

- `Id`
- `StepNumber`
- `Description`
- `DescriptionHtml`
- `TimerMinutes`
- `ImageBase64`
- `Links`

`StepIngredientLink`

- `IngredientId`
- `DisplayText`

### Menu model

`MenuPlan`

- `Id`
- `Name`
- `StartDate`
- `EndDate`
- `Notes`
- `CreatedAt`
- `Days`

`MenuDay`

- `Date`
- `Slots`

`MenuSlot`

- `SlotType`
- `RecipeIds`
- old `RecipeId` migration field
- `Notes`

### Ingredient catalogue

`ingredients-db.json`

- 12 categories.
- 157 ingredient records.
- Synonyms/aliases in Catalan, Spanish, and some English/French terms.

One duplicate canonical name detected:

- `coriandre`

### Saved data export

`input/recetari_data.json`

- Recipes: 73.
- Complete recipes: 21.
- Meal ideas/placeholders: 52.
- Menus: 3.
- Custom ingredients: 2.
- Last saved: `2026-05-10T13:46:44.834+02:00`.

## Data migration viability

### Can migrate now

- Static ingredient categories.
- Static ingredient aliases.
- Ingredient matching concepts.
- Some default recipe categories from `CreateDefaultData`.
- Saved `recetari_data.json`.
- 21 complete saved recipes.
- 52 meal ideas/placeholders.
- 3 saved menus.
- 2 custom ingredients.

### Should not migrate

- OAuth token/session behavior.
- Google Drive file ID as application state.
- Build output in `bin`, `obj`, `.vs`.
- `client_secret_*.json`.
- Base64 images inside JSON as the long-term media strategy.

## What is genuinely reusable

### Strong reuse

`wwwroot/data/ingredients-db.json`

- Good seed for `Ingredients` and `IngredientAliases`.
- Useful balance categories: fish, meat, eggs/dairy, vegetables, fruit, legumes.
- Multilingual aliases are valuable for future import.

`input/recetari_data.json`

- Complete recipe records with structured ingredient quantities/units.
- Saved meal-plan/menu data.
- Meal ideas that reveal real family planning patterns.
- Good evidence for Template Recipes: rotllets de primavera, iogurt bowls, amanides, truita francesa, samuses, llenties amb verduretes.

### Partial reuse

`IngredientDbService`

- Useful normalization and scoring idea.
- Needs better Unicode normalization and stronger false-positive handling.
- Should become an import/matching service, not UI state.

`IngredientScalingService`

- Useful distinction between continuous units, approximate units, whole units, and no quantity.
- Should be rewritten around a target `Units` table and raw quantity preservation.

`MenuPlan` / `MenuSlot`

- Useful proof that weekly planning matters.
- Must be expanded to connect with pantry, freezer, thawing, shopping, meal history, and balance rules.

`RecipeStep.TimerMinutes`

- Worth keeping in the target model.

Recipe categories and menu slots:

- Useful as a starting point for `RecipeRoles`.
- Need to be split from balance roles, cooking roles, and template/family membership.

### Weak reuse

Blazor pages and MudBlazor components:

- Fine as a prototype reference.
- Not worth preserving as product foundation unless the future product deliberately stays Blazor.

Rich ingredient-step linking:

- Interesting but too manual for import-heavy workflows.
- Better as optional annotation after recipe import.

Missing model concepts:

- `BasePreparations`
- `RecipeFamilies`
- `TemplateRecipes`
- `CookingTechniques`
- `KitchenKnowledgeObjects`
- `PrepLeadTimeRules`
- `LeftoverRules`
- `ServingSuggestions`
- `MealIdeas`

## Why it likely felt tedious

- Manual recipe creation required too many structured fields up front.
- Ingredient entry required name, quantity, unit, category, and often autocomplete confirmation.
- Ingredient-step linking required selecting text and linking manually.
- No bulk import from DOCX or pasted recipe text.
- No review queue for messy recipes.
- Menu planning did not automatically reason about pantry/freezer/shopping/thawing.
- Weekly balance was not part of planning.
- Google Drive login/setup added friction.
- The single JSON file storage model is simple but not ergonomic for conflict handling, partial updates, audit, or mobile offline behavior.
- Recipe data was the center of the app, but the desired product is a kitchen ERP where stock, plans, shopping, and history are equal citizens.

## Recommendation

Reuse XupXup as a migration source and reference implementation only.

Priority reuse order:

1. Import `ingredients-db.json`.
2. Convert aliases to `IngredientAliases`.
3. Preserve scaling behavior as test cases.
4. Import `recetari_data.json` through staging.
5. Split complete recipes from meal ideas/placeholders.
6. Leave the Blazor/Google Drive app behind.
