# 05 - Design Decisions

Generated: 2026-06-26

Each decision is evaluated against the mental-load principle:

> Does this reduce remembering, deciding, checking, planning, or re-entry for the family?

## Decision 1 - The Product Is A Kitchen ERP, Not A Recipe App

Alternatives:

- A recipe manager with extra features.
- A full kitchen ERP centered on planning and household state.

Recommendation:

- Build the domain as a kitchen ERP.

Why:

- Recipes alone do not answer "what should we eat tonight?"
- Mental load lives at the intersection of food knowledge, stock, preferences, time, shopping, and planning.

Implication:

- Recipe is important but not the central aggregate of the whole system.

## Decision 2 - Keep Meal Ideas Separate From Recipes

Alternatives:

- Store every meal idea as a recipe.
- Store meal ideas separately.

Recommendation:

- Add `MealIdea`.

Why:

- XupXup contains 52 records without ingredients or steps.
- They are useful for planning but false as recipes.

Mental-load benefit:

- The family can capture ideas quickly without completing a form.

## Decision 3 - Use Recipe Families And Templates

Alternatives:

- Four spring roll recipes.
- One recipe with notes.
- Recipe family plus template plus variants.

Recommendation:

- Use `RecipeFamily` for grouping.
- Use `TemplateRecipe` when the pattern has reusable slots.

Why:

- Reduces duplicated recipe maintenance.
- Supports examples like:
  - Spring rolls with chicken/turkey/duck/beef.
  - Oven chicken with lemon/beer/Mediterranean/spices.
  - Salads with base/protein/extras/dressing.

Mental-load benefit:

- The family chooses a pattern, then a variation.

## Decision 4 - Use A Generic Preparation Entity

Alternatives:

- Model tomato sauce, bechamel, dough, stock, marinade as separate entity types.
- Treat them all as recipes.
- Use one `Preparation` concept with type and behavior.

Recommendation:

- Use `Preparation`.

Why:

- These objects share reuse and storage behavior but differ in details.
- Separate subclasses would over-engineer too early.

Mental-load benefit:

- The family can batch, store, reuse, and plan around bases.

## Decision 5 - Split Classification Into Specific Concepts

Alternatives:

- One universal tag table.
- Many explicit classification concepts.

Recommendation:

- Use explicit concepts where behavior differs:
  - MealRole.
  - NutritionBalanceRole.
  - PlanningAttribute.
  - DishCategory.
  - MealContext.

Why:

- `fish`, `starter`, `quick`, and `hot weather` do not mean the same thing.

Mental-load benefit:

- Recommendations are explainable and less noisy.

## Decision 6 - Pantry Tracking Should Be Approximate By Default

Alternatives:

- Exact quantities for everything.
- No pantry tracking.
- Approximate tracking with exact lots where useful.

Recommendation:

- Approximate by default; exact where it matters.

Why:

- Exact stock accounting is tedious.
- Freezer lots and perishables need more precision than salt or flour.

Mental-load benefit:

- The family gets useful reminders without maintaining a warehouse system.

## Decision 7 - Freezer Lots Are First-Class

Alternatives:

- Treat freezer as pantry location.
- Model freezer lots separately.

Recommendation:

- Use `FreezerLot`.

Why:

- FEFO, frozen date, best-before, thawing, and safe-use status are lot-specific.

Mental-load benefit:

- The family no longer has to remember what is buried in the freezer.

## Decision 8 - Equipment Belongs To The Domain

Alternatives:

- Store equipment as recipe notes.
- Model equipment directly on recipes.
- Model equipment and techniques separately.

Recommendation:

- Model `Equipment`.
- Link techniques to equipment.
- Link recipes/templates to techniques, with optional direct equipment overrides.

Why:

- A recipe is feasible only if the kitchen has the required tools.

Mental-load benefit:

- The app can avoid suggesting impossible or annoying meals.

## Decision 9 - Seasonality Is A Context Problem

Alternatives:

- Month ranges on recipes.
- Tags.
- `MealContext`.

Recommendation:

- Use `MealContext`, with season as one kind of context.

Why:

- Real planning depends on heat, holidays, guests, busy weeks, school, picnics, and children at home.

Mental-load benefit:

- The app can suggest food that fits life, not only the calendar.

## Decision 10 - Nutrition Is A Signal, Not A Ledger

Alternatives:

- Exact calorie/macro tracking.
- No nutrition model.
- Approximate signals and weekly balance.

Recommendation:

- Use `NutritionSignal` plus `BalanceRule`.

Why:

- The stated goal is approximate warnings, not calorie counting.

Mental-load benefit:

- The family gets guidance without food logging anxiety.

## Decision 11 - Import Staging Is Permanent

Alternatives:

- Import directly into canonical tables.
- Use temporary scripts only.
- Keep import/curation as a domain context.

Recommendation:

- Keep `ImportSource`, candidates, duplicate review, and promotion decisions.

Why:

- Future input will be messy: DOCX, websites, photos, AI, pasted text, old JSON.

Mental-load benefit:

- Capture first, clean later.

## Decision 12 - Substitutions Should Be Structured

Alternatives:

- Store substitution notes in recipe text.
- Model structured substitutions.

Recommendation:

- Add `Substitution`.

Why:

- "Use turkey instead of chicken" differs from "replace dairy with plant milk".
- Some substitutions affect technique, flavor, nutrition, or shopping.

Mental-load benefit:

- The app can help when the exact ingredient is missing.

## Decision 13 - Keep Health Data Minimal

Alternatives:

- Track full weight/height/nutrition history.
- Ignore personal health.
- Keep optional dining profiles and soft signals.

Recommendation:

- Keep `DiningProfile` but make sensitive health fields optional and minimal.

Why:

- This is a family planning app, not a medical tracker.

Mental-load benefit:

- Supports useful constraints without making the app heavy or invasive.

## Decision 14 - Commercial Quality Does Not Mean Maximum Scope

Alternatives:

- Build everything now.
- Build a domain that can grow but starts with high-leverage concepts.

Recommendation:

- Model durable concepts now.
- Implement only mental-load reducers first.

Why:

- Over-engineering recreates the tedium the product is trying to remove.

## Deferred Decisions

Defer:

- Full nutrient database.
- Barcode/product catalog.
- Multi-store price optimization.
- Automatic AI meal generation as canonical recipes.
- User account/sync/security architecture.
- Exact portion tracking per person.

Reason:

- They may matter later, but they are not required to validate the core mental-load reduction loop.

