# Receptari dels Nasdanus - Target Data Model

Generated: 2026-06-26

Implementation update: 2026-07-29

This document describes the desired richer domain model. The current implemented runtime is simpler and static: Blazor WebAssembly plus `localStorage`, with local JSON knowledge files. See `docs/implementation_status.md` for the implemented model and code map.

Current implemented equivalents:

- `Ingredients` exist as local ingredient knowledge plus browser state.
- `IngredientAliases` are stored in local knowledge JSON and copied into app state.
- `RecipeIngredients` preserve raw name, quantity, unit and an optional link to a known ingredient.
- Nutrition belongs to known ingredients as values per 100 g.
- Manual nutrition is stored on the ingredient with source `manual`.
- Meal planning uses weekly `MealPlanSlot` records with lunch/dinner planned recipes.
- Product feedback is represented by `ProductBacklogItem` and stored in local app state.
- Data export/import is the portability mechanism for now.

## Design principles

- Local-first: the device owns the full working database.
- Normalized core, tolerant import edge: keep canonical tables clean, but allow messy imported text in staging.
- Preserve raw text: never discard original ingredient lines, step text, source notes, or import provenance.
- Approximate nutrition only: kcal should produce soft warnings, not exact calorie counting.
- Weekly balance is role-based: fish, chicken, meat, eggs, legumes, vegetables, fruit.
- Pantry/freezer/shopping/meal planning are one workflow, not separate apps.
- Android portability: keep the domain and persistence model independent of any future UI framework.

Recommended storage: SQLite with migrations. Add sync later using a change log or explicit export/import snapshots. Do not make Google Drive or any cloud service the primary database.

## Core entities

### Family

Purpose: household/account boundary.

Fields:

- `id`
- `name`
- `home_timezone`
- `default_locale`
- `created_at`
- `updated_at`

Relationships:

- Has many `FamilyMembers`.
- Owns pantry, freezer, recipes, meal plans, shopping lists, and rules.

### FamilyMembers

Purpose: people whose meals/preferences affect planning.

Fields:

- `id`
- `family_id`
- `name`
- `role` (`adult`, `child`, `guest`, etc.)
- `birth_year` nullable
- `active`
- `notes`

Relationships:

- Has one or more `DiningProfile` records.
- Can be attached to meal servings/preferences.

### DiningProfile

Purpose: planning constraints and soft health preferences.

Fields:

- `id`
- `family_member_id`
- `profile_name`
- `dietary_flags` JSON or join table
- `allergen_flags` JSON or join table
- `disliked_ingredient_ids` join table
- `preferred_ingredient_ids` join table
- `approx_kcal_warning_min`
- `approx_kcal_warning_max`
- `portion_multiplier`
- `notes`

Notes:

- Store warnings as ranges/bands.
- Do not model exact calorie targets unless the product direction changes.

### Ingredients

Purpose: canonical ingredient catalogue.

Fields:

- `id`
- `canonical_name`
- `category_id`
- `parent_ingredient_id` nullable
- `default_unit_id` nullable
- `shopping_unit_id` nullable
- `pantry_unit_id` nullable
- `density_or_conversion_notes`
- `approx_kcal_per_100g` nullable
- `balance_group` nullable (`fish`, `chicken`, `meat`, `eggs`, `legumes`, `vegetables`, `fruit`, `other`)
- `is_pantry_staple_default`
- `is_freezable_default`
- `season_start_month` nullable
- `season_end_month` nullable
- `notes`

Supporting table: `IngredientAliases`

- `id`
- `ingredient_id`
- `alias`
- `language`
- `source`
- `confidence`

Migration note: seed from XupXup `ingredients-db.json`; merge duplicate `coriandre`.

### PantryItems

Purpose: current shelf/fridge stock and low-stock behavior.

Fields:

- `id`
- `family_id`
- `ingredient_id`
- `display_name_override` nullable
- `location` (`pantry`, `fridge`, `spice_drawer`, etc.)
- `quantity`
- `unit_id`
- `opened_at` nullable
- `best_before` nullable
- `low_threshold_quantity`
- `low_threshold_unit_id`
- `auto_add_to_shopping_list`
- `notes`
- `updated_at`

Behavior:

- If quantity drops below threshold and `auto_add_to_shopping_list` is true, create or update a shopping-list item.
- Pantry quantities can be approximate. The UI should allow `low`, `ok`, `plenty` if exact values are too heavy.

### FreezerLots

Purpose: track frozen batches/lots and support FEFO.

Fields:

- `id`
- `family_id`
- `ingredient_id` nullable
- `recipe_id` nullable
- `base_preparation_id` nullable
- `label`
- `quantity`
- `unit_id`
- `servings_estimate` nullable
- `frozen_on`
- `best_before`
- `use_by` nullable
- `freezer_location`
- `lot_status` (`available`, `planned_to_thaw`, `thawing`, `thawed`, `used`, `discarded`)
- `thaw_started_at` nullable
- `safe_until` nullable
- `notes`

Behavior:

- FEFO sort uses `best_before`, then `frozen_on`.
- A freezer lot can represent raw ingredient, cooked recipe, or base preparation.

### BasePreparations

Purpose: reusable preparations that are not always meals.

Examples from source data:

- Massa mare.
- Salsa de tomàquet.
- Beixamel.
- Gremolada.
- Crema catalana as filling/base in another dessert.

Fields:

- `id`
- `family_id`
- `name`
- `description`
- `yield_quantity`
- `yield_unit_id`
- `storage_instructions`
- `fridge_life_days` nullable
- `freezer_life_days` nullable
- `source_recipe_id` nullable
- `notes`

Supporting tables:

- `BasePreparationIngredients`
- `BasePreparationSteps`

### Recipes

Purpose: canonical cookable recipes.

Fields:

- `id`
- `family_id`
- `name`
- `slug`
- `description`
- `source_status` (`active`, `draft`, `needs_review`, `archived`)
- `source_category`
- `servings`
- `yield_quantity` nullable
- `yield_unit_id` nullable
- `prep_time_minutes` nullable
- `cook_time_minutes` nullable
- `rest_time_minutes` nullable
- `difficulty` nullable
- `season_start_month` nullable
- `season_end_month` nullable
- `freezer_friendly`
- `leftover_friendly`
- `recipe_kind` (`canonical`, `template`, `variant`, `component`, `draft`)
- `approx_kcal_per_serving_min` nullable
- `approx_kcal_per_serving_max` nullable
- `kcal_warning_band` nullable (`low`, `normal`, `rich`, `very_rich`)
- `source_id` nullable
- `created_at`
- `updated_at`

Supporting tables:

- `RecipeSteps`: ordered steps, optional timer, optional media, raw text.
- `RecipeNotes`: tips, provenance, substitutions, child notes.
- `RecipeSources`: DOCX paragraph range, URL, XupXup ID, import timestamp.

### RecipeIngredients

Purpose: structured ingredients for a recipe, while preserving messy originals.

Fields:

- `id`
- `recipe_id`
- `ingredient_id` nullable until reviewed
- `base_preparation_id` nullable
- `raw_text`
- `quantity_min` nullable
- `quantity_max` nullable
- `unit_id` nullable
- `preparation_note` nullable (`ratllat`, `picat`, `dessalat`, etc.)
- `optional`
- `pantry_staple`
- `shopping_behavior` (`always`, `if_missing`, `never`, `ask`)
- `freezer_source_allowed`
- `order_index`
- `confidence`
- `needs_review`

Notes:

- Keep `raw_text` even after normalization.
- Allow quantity ranges and vague values such as `al gust`.

### RecipeRoles

Purpose: planning and weekly balance classification.

Fields:

- `id`
- `recipe_id`
- `role`
- `strength` (`primary`, `secondary`, `minor`)
- `source` (`manual`, `ingredient_inferred`, `imported`)

Core roles:

- `fish`
- `chicken`
- `meat`
- `eggs`
- `legumes`
- `vegetables`
- `fruit`

Additional useful roles:

- `bread_or_dough`
- `dessert`
- `sauce`
- `base_preparation`
- `quick`
- `seasonal`
- `freezer_friendly`

### RecipeAdaptations

Purpose: model variants without cloning everything blindly.

Fields:

- `id`
- `base_recipe_id`
- `adapted_recipe_id` nullable
- `name`
- `adaptation_type` (`diet`, `ingredient_swap`, `seasonal`, `child_friendly`, `improved_version`, `equipment`)
- `description`
- `changes_json`
- `active`

Examples:

- `Magdalenes Dukan` as possible diet adaptation.
- `Coca de Sant Joan (versió millorada)` as possible improved version.
- `Canelons de carbassó amb formatge` as variant of a canelons family.

### RecipeFamilies

Purpose: group recipes that are variants, versions, hero-ingredient families, or template implementations.

Fields:

- `id`
- `family_id`
- `name`
- `family_type` (`template`, `variant_group`, `hero_ingredient`, `seasonal_group`, `technique_group`)
- `template_recipe_id` nullable
- `description`
- `active`

Supporting table: `RecipeFamilyMembers`

- `id`
- `recipe_family_id`
- `recipe_id`
- `member_role` (`template`, `variant`, `version`, `base`, `example`, `archived_duplicate`)
- `sort_order`
- `notes`

Examples from the migration:

- `Canelons de carbassó` with seafood and cheese variants.
- `Coca de Sant Joan` original/improved versions.
- `Pans i masses fermentades` with massa mare and bread variants.
- `Salses emulsionades` with mahonesa/lactonesa/burger sauce.

### TemplateRecipes

Purpose: reusable recipe patterns with slots that variants fill.

Fields:

- `id`
- `family_id`
- `name`
- `description`
- `default_role`
- `default_technique_id` nullable
- `source_recipe_id` nullable
- `active`

Supporting table: `TemplateSlots`

- `id`
- `template_recipe_id`
- `slot_name`
- `slot_type` (`ingredient`, `base_preparation`, `technique`, `sauce`, `filling`, `wrapper`, `topping`, `side`, `timing`)
- `required`
- `default_ingredient_id` nullable
- `default_base_preparation_id` nullable
- `notes`

Supporting table: `RecipeVariantSlotValues`

- `id`
- `recipe_id`
- `template_slot_id`
- `ingredient_id` nullable
- `base_preparation_id` nullable
- `technique_id` nullable
- `raw_value`
- `notes`

Examples:

- Template: `Rotllets de primavera`; variants can fill protein slot with pollastre, ànec, gall dindi, vedella.
- Template: `Pollastre al forn`; variants can fill flavor slot with llimona, mediterrani, cervesa, espècies.
- Source candidate: `Canelons de carbassó`; slots include wrapper, filling, sauce, gratin topping.

### CookingTechniques

Purpose: reusable methods and equipment/process tags.

Fields:

- `id`
- `name`
- `category` (`heat_method`, `equipment_method`, `prep_lead_time`, `texture`, `sauce_technique`, `dough_process`, etc.)
- `default_equipment`
- `active_time_level` nullable
- `attention_level` nullable
- `notes`

Supporting table: `RecipeTechniques`

- `id`
- `recipe_id`
- `technique_id`
- `strength` (`primary`, `secondary`, `minor`)
- `evidence`
- `source` (`manual`, `import_inferred`)

Techniques found in the DOCX include oven, gratin, air fryer, wok, pan saute, cassola/guisat, boiling, steaming, frying, roasting, marinating, blending, emulsifying, fermentation, and bain-marie.

### KitchenKnowledgeObjects

Purpose: reusable kitchen rules that are neither ingredients nor recipes.

Fields:

- `id`
- `family_id`
- `object_type` (`prep_lead_time_rule`, `serving_rule`, `storage_rule`, `leftover_rule`, `seasonality_rule`, `adaptation_rule`, `safety_rule`)
- `name`
- `description`
- `applies_to_recipe_id` nullable
- `applies_to_family_id` nullable
- `applies_to_ingredient_id` nullable
- `applies_to_base_preparation_id` nullable
- `trigger_json`
- `action_json`
- `active`

Examples:

- Soak legumes the night before.
- Desalt bacallà with water changes.
- Maintain massa mare in the fridge and refresh before use.
- Serve bread with saucy dishes.
- Blend onion soup for children.
- Use rostit leftovers for canelons or croquetes.
- Surface seasonal recipes around Sant Joan, Setmana Santa, Tots Sants, and Sant Josep.

### MealPlan

Purpose: future planned meals.

Fields:

- `id`
- `family_id`
- `week_start_date`
- `name`
- `status` (`draft`, `active`, `completed`, `archived`)
- `notes`
- `created_at`
- `updated_at`

Supporting table: `MealPlanSlots`

- `id`
- `meal_plan_id`
- `date`
- `slot` (`breakfast`, `mid_morning`, `lunch`, `snack`, `dinner`)
- `recipe_id` nullable
- `base_preparation_id` nullable
- `freezer_lot_id` nullable
- `servings_planned`
- `notes`

Behavior:

- Slots can hold recipes, freezer lots, leftovers, or free-text placeholders.
- Planning should update shopping suggestions and thawing plan.

### MealIdeas

Purpose: lightweight meal concepts that are useful for planning but are not complete recipes yet.

Fields:

- `id`
- `family_id`
- `name`
- `description`
- `source`
- `source_ref_id`
- `status` (`idea`, `draft_recipe`, `promoted`, `archived`)
- `suggested_slot` nullable (`breakfast`, `lunch`, `dinner`, `snack`)
- `recipe_family_id` nullable
- `template_recipe_id` nullable
- `balance_role_hints` JSON
- `notes`

Behavior:

- XupXup records with no ingredients and no steps should land here first.
- A meal idea can be used in a meal plan before it becomes a full recipe.
- Promotion to `Recipes` should require at least review of ingredients, steps, and role tags.

### MealHistory

Purpose: what was actually eaten/cooked.

Fields:

- `id`
- `family_id`
- `date`
- `slot`
- `recipe_id` nullable
- `freezer_lot_id` nullable
- `servings`
- `was_leftovers`
- `rating` nullable
- `notes`
- `created_from_meal_plan_slot_id` nullable

Behavior:

- Feeds weekly balance reports.
- Helps avoid repeating recipes too often.
- Can update pantry/freezer stock if the user opts in.

### BalanceRules

Purpose: household-level weekly planning rules.

Fields:

- `id`
- `family_id`
- `name`
- `period` (`week`)
- `role`
- `min_count`
- `max_count`
- `severity` (`info`, `warning`, `strong_warning`)
- `applies_to_slots` JSON
- `active`

Example defaults:

- Fish: min 2/week.
- Chicken: max or target 1-2/week.
- Meat: max 1-2/week.
- Eggs: target 1-2/week.
- Legumes: min 2/week.
- Vegetables: daily.
- Fruit: daily or meal-adjacent reminder.

Warnings should be approximate and explainable.

### ShoppingList

Purpose: list generated from plan, pantry, low stock, and manual additions.

Fields:

- `id`
- `family_id`
- `name`
- `status` (`open`, `shopping`, `completed`, `archived`)
- `created_at`
- `completed_at` nullable

Supporting table: `ShoppingListItems`

- `id`
- `shopping_list_id`
- `ingredient_id` nullable
- `raw_name`
- `quantity`
- `unit_id`
- `source` (`manual`, `meal_plan`, `pantry_low`, `recipe_import`, `freezer_restock`)
- `source_ref_id` nullable
- `checked`
- `store_section` nullable
- `notes`

Behavior:

- Merge duplicate ingredients by canonical ingredient and compatible unit.
- Keep raw names for unreviewed imported ingredients.
- Low pantry threshold can create or update list items.

### ThawingPlan

Purpose: planned thawing actions for freezer lots.

Fields:

- `id`
- `family_id`
- `freezer_lot_id`
- `meal_plan_slot_id` nullable
- `recipe_id` nullable
- `planned_use_at`
- `recommended_thaw_start_at`
- `actual_thaw_start_at` nullable
- `safe_until` nullable
- `status` (`planned`, `started`, `ready`, `used`, `cancelled`, `expired`)
- `method` (`fridge`, `room_temp_short`, `microwave`, `cook_from_frozen`)
- `notes`

Behavior:

- Generated from meal plan slots that use freezer lots or frozen ingredients.
- Warn when a lot should be moved from freezer to fridge.
- Prefer FEFO lots when suggesting what to use.

## Import and provenance support

Add these supporting tables early:

### ImportSources

- `id`
- `source_type` (`docx`, `xupxup_json`, `manual_paste`, `web`, `photo_ocr`)
- `source_path_or_url`
- `imported_at`
- `checksum`
- `notes`

### RecipeImportCandidates

- `id`
- `import_source_id`
- `raw_name`
- `raw_category`
- `raw_description`
- `raw_steps_json`
- `raw_notes_json`
- `raw_ingredient_candidates_json`
- `duplicate_candidate_recipe_id` nullable
- `review_status`
- `promoted_recipe_id` nullable

This is the buffer that keeps future recipe import easy without polluting canonical tables.

## Core workflows

### Weekly planning

1. User selects recipes/freezer lots/placeholders.
2. App checks `RecipeRoles` against `BalanceRules`.
3. App checks pantry and freezer availability.
4. App generates shopping suggestions and thawing actions.
5. User accepts/edits suggestions.

### Running low

1. Pantry item crosses threshold or user marks low.
2. App creates a `ShoppingListItem` with source `pantry_low`.
3. If item already exists, quantity/source notes are merged.

### FEFO freezer

1. Meal plan asks for an ingredient or recipe available in freezer.
2. App suggests oldest `best_before` lot first.
3. Accepted lot creates or updates `ThawingPlan`.
4. Used lot moves to `used` and can create `MealHistory`.

### Approx kcal warnings

1. Recipe stores approximate kcal range per serving.
2. Dining profile stores warning bands.
3. Meal plan displays soft warning only when the band is outside family preferences.
4. No exact calorie ledger is required.
