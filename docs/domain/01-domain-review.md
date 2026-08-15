# 01 - Domain Review

Generated: 2026-06-26

## Governing Principle

The goal is not to build the most complete cooking application.

The goal is to reduce the family's mental load around cooking.

Every concept in the domain should earn its place by reducing one or more of these burdens:

- Remembering what exists at home.
- Deciding what to cook.
- Checking whether a meal is balanced enough.
- Knowing what must be bought.
- Knowing what must be thawed or used first.
- Avoiding repeated manual entry.
- Preserving family kitchen knowledge so it does not live only in someone's head.

If a concept is technically elegant but does not reduce remembering, deciding, checking, planning, or re-entry, it should be deferred.

## Evidence Reviewed

Artifacts reviewed:

- `output/extracted_recipes.json`
- `output/xupxup_saved_recipes.json`
- `output/ingredient_candidates.json`
- `output/duplicate_candidates.json`
- `output/recipe_family_candidates.json`
- `output/reusable_kitchen_objects.json`

Key evidence:

- DOCX source: 62 extracted recipes, 6 source categories, no reliable ingredient sections.
- XupXup saved data: 73 records, 21 complete recipes, 52 meal ideas/placeholders, 3 menus.
- Ingredient candidates: 90 matched DOCX catalogue ingredients, 39 unmatched DOCX terms, 101 structured ingredient names from XupXup saved recipes.
- Duplicate/family candidates: 19 duplicate/similarity pairs, 17 recipe family candidates, 13 template recipe candidates.
- Reusable objects: 20 base-preparation candidates, 15 cooking technique candidates, 9 kitchen knowledge candidates.

## Critical Review Of Previous Work

### 1. The model was still too recipe-centric

Previous reports correctly moved beyond recipes, but `Recipes` still had too much gravitational pull. Many XupXup records are not recipes at all:

- `Iogurt amb kiwi`
- `Amanida verda`
- `Rotllets de primavera amb pavo`
- `Truita francesa amb crema de carbassó`

These reduce mental load when used as meal ideas, not when forced into full recipe forms.

Correction:

- Keep `Recipe` for repeatable cooking instructions.
- Add `MealIdea` for low-friction planning.
- Add `TemplateRecipe` for patterns.
- Add `RecipeFamily` for grouping variants.

### 2. Ingredient extraction from the DOCX must remain non-authoritative

The DOCX usually embeds ingredients inside prose. The extraction can say "this recipe probably mentions tomato", but it cannot safely produce a structured ingredient list.

Risk:

- If inferred ingredients become canonical too early, shopping lists and pantry matching will become unreliable.

Correction:

- DOCX ingredients stay in `IngredientImportCandidate`.
- XupXup structured ingredients get higher confidence, but still require unit/name review.
- Canonical `RecipeIngredient` should require review status.

### 3. `RecipeRoles` risk becoming a junk drawer

The earlier model used roles for weekly balance, meal position, dish type, and convenience. These are different concepts.

Examples:

- `fish` is a weekly balance role.
- `starter` is a meal course role.
- `make_ahead` is an operational planning hint.
- `dessert` is a dish category.
- `quick` is effort/time metadata.

Correction:

- Split into separate concepts:
  - `NutritionBalanceRole`
  - `MealRole`
  - `PlanningAttribute`
  - `DishCategory`

Do not force all classification into one table just because it is easy to query.

### 4. `BasePreparation` needs sharper boundaries

The previous model listed many candidates: beixamel, sofregit, vinagreta, massa mare, cooked rice, caramel, marinades, doughs. These are not all the same kind of thing.

They share one important property: they can be reused.

But they differ by inventory behavior:

- Tomato sauce can be cooked, stored, frozen, bought as a product, or used as an ingredient.
- Cooked rice is a prepared food with short life and leftover behavior.
- Marinade may be a formula, not usually an inventory item.
- Dough can be a preparation, a freezer lot, or an intermediate step.

Correction:

- Use a generic `Preparation` aggregate with a `preparation_type`.
- A preparation may optionally create inventory.
- A preparation may optionally be used inside recipes.
- Avoid one subclass per preparation type.

### 5. Pantry model should not become accounting

A family kitchen does not need perfect stock accounting for every item.

Mental-load goal:

- Know whether we have enough.
- Know what is running low.
- Know what should be used soon.
- Know what is frozen and when to thaw it.

Risk:

- Exact quantities for everything will recreate the tedium of XupXup.

Correction:

- Support exact quantities when useful.
- Also support approximate levels: `none`, `low`, `ok`, `plenty`.
- Use thresholds for staples.
- Use lot-level tracking mainly where it matters: freezer, perishables, batch cooking.

### 6. Equipment was under-modeled

XupXup and the DOCX mention air fryer, oven, pan, wok, vaporera, blender, and other methods. Equipment is not just a note.

Why it matters:

- A recipe requiring an air fryer is easier on some days and impossible in kitchens without one.
- Children may be able to help with some equipment but not others.
- Techniques depend on equipment.

Correction:

- Add `Equipment`.
- Link `Technique` to required or preferred equipment.
- Link recipes/templates to techniques, not only directly to equipment.

### 7. Seasonality was too narrow

Month ranges are not enough.

The family will ask:

- What is good in hot weather?
- What is good when there are guests?
- What is good for lunch boxes?
- What is good for busy weeks?
- What is traditional for Sant Joan?

Correction:

- Model `MealContext`, not only `Season`.
- A context can represent weather, holiday, weekday pressure, guests, picnic, children at home, school lunch, batch cooking, or celebration.

### 8. Nutrition must be advisory, not controlling

The earlier model included approximate kcal ranges and weekly balance. That is appropriate, but it must not drift into calorie counting.

Mental-load goal:

- "This week lacks fish."
- "This meal is quite rich."
- "Maybe add fruit/vegetables."

Correction:

- Use `NutritionSignal` and `BalanceRule`.
- Represent green/orange/red or low/normal/rich.
- Avoid per-person exact calorie ledgers unless explicitly requested later.

### 9. Family profile was under-specified

The app may eventually support multiple families. Preferences cannot belong only to recipes or ingredients.

Examples:

- A child dislikes a texture, not an ingredient.
- A family prefers fast dinners during school weeks.
- Someone may like chicken but not curry.
- A technique may be preferred or avoided.

Correction:

- Preferences can target ingredients, recipes, techniques, textures, contexts, and meal slots.
- Keep personal health data optional and minimal.

### 10. Import staging is a domain need, not just a migration tool

Future AI import, pasted recipes, photos, websites, and WhatsApp notes will all be messy.

Correction:

- Keep `ImportSource`, `RecipeImportCandidate`, `IngredientImportCandidate`, and `PromotionDecision`.
- Treat import/review as a permanent bounded context.

## Concepts That Should Not Exist Yet

These are tempting but should be deferred:

- Full nutrient database.
- Exact macro tracking.
- Complex multi-store shopping optimization.
- Automated recipe generation as canonical data.
- Fine-grained stock accounting for every pantry item.
- Separate entity types for every preparation subtype.
- A rigid ontology of every cuisine/diet/style.

They may be useful someday, but today they increase complexity faster than they reduce mental load.

## Missing Concepts To Add

High-value missing concepts:

- `MealIdea`
- `RecipeFamily`
- `TemplateRecipe`
- `Preparation`
- `Technique`
- `Equipment`
- `KitchenKnowledge`
- `MealContext`
- `PlanningAttribute`
- `NutritionSignal`
- `Preference`
- `InventoryPolicy`
- `UseSoonSignal`
- `PromotionDecision`

## Domain Review Conclusion

The correct domain is not "recipes plus inventory".

The correct domain is:

> A family kitchen planning system that turns household preferences, reusable kitchen knowledge, available stock, freezer lots, and recipe patterns into low-friction meal decisions.

Recipes are important, but they are not the center of the domain. The center is reduced decision burden.

