# 06 - Future Roadmap

Generated: 2026-06-26

This roadmap is domain-first. It avoids implementation choices until the domain has been validated.

## Roadmap Principle

Build in the order that reduces family mental load fastest.

Do not build features merely because they are possible.

## Phase 1 - Domain Stabilization

Goal:

- Agree on the core language.

Work:

- Confirm the entity catalog.
- Review open questions.
- Decide which XupXup placeholders become MealIdeas.
- Decide which complete XupXup records become Recipes.
- Confirm TemplateRecipe and Preparation boundaries.

Exit criteria:

- The family can explain the difference between Recipe, MealIdea, Preparation, and TemplateRecipe.

## Phase 2 - Migration Staging

Goal:

- Preserve all existing knowledge safely.

Work:

- Stage DOCX recipes.
- Stage XupXup complete recipes.
- Stage XupXup meal ideas.
- Stage ingredients and aliases.
- Stage duplicates and family candidates.
- Record promotion decisions.

Exit criteria:

- No source information is lost.
- Canonical data remains reviewed and trustworthy.

## Phase 3 - Mental-Load MVP

Goal:

- Help the family decide meals with less effort.

Minimum domain loop:

- MealIdeas.
- Recipes.
- Basic meal plan.
- Pantry low markers.
- Freezer lots.
- Shopping list.
- Thawing reminders.
- Weekly balance signals.

Out of scope:

- Full nutrition.
- Complex AI.
- Perfect pantry quantities.
- Polished recipe import.

Exit criteria:

- The family can plan a week, generate a shopping list, and know what to thaw.

## Phase 4 - Pantry And Freezer Reliability

Goal:

- Make "what do we have?" reliable enough.

Work:

- Inventory policies.
- Approximate pantry levels.
- Freezer FEFO.
- Prepared lots.
- Use-soon signals.
- Recurring purchases.

Exit criteria:

- The family trusts freezer and low-stock suggestions.

## Phase 5 - Reusable Kitchen Knowledge

Goal:

- Stop repeating knowledge inside recipes.

Work:

- Promote base preparations.
- Promote techniques.
- Add equipment.
- Add kitchen knowledge rules.
- Add substitutions.
- Add serving advice.

Exit criteria:

- The app can explain reusable advice like soaking, desalting, thawing, bread-with-sauce, and child-friendly adaptations.

## Phase 6 - Template Recipes And Families

Goal:

- Reduce recipe duplication.

Work:

- Promote recipe families.
- Promote template recipes.
- Define slots.
- Attach variants.
- Convert repetitive meal ideas into templates.

Priority candidates:

- Rotllets de primavera.
- Amanides ràpides.
- Iogurt bowls.
- Truita francesa.
- Samuses.
- Llenties amb verduretes.
- Canelons de carbassó.
- Pans i masses fermentades.

Exit criteria:

- New variants can be added without duplicating entire recipes.

## Phase 7 - Recommendations

Goal:

- Help choose, not just store.

Work:

- Context-aware suggestions.
- Inventory-aware suggestions.
- Balance-aware suggestions.
- Time/effort-aware suggestions.
- Child-help suggestions.

Questions supported:

- What can I cook without shopping?
- What should I use soon?
- What should I thaw?
- What fits hot weather?
- What can Cora help with?

Exit criteria:

- Recommendations are few, explainable, and useful.

## Phase 8 - Future AI Assistant

Goal:

- Let AI use the domain safely.

Work:

- Queryable canonical concepts.
- Provenance for imported data.
- Review workflow for AI suggestions.
- Explanation layer for recommendations.

Rule:

- AI can suggest.
- Canonical data changes require review.

Exit criteria:

- AI answers are grounded in family inventory, preferences, and reviewed data.

## Phase 9 - Platform And Sync

Goal:

- Make the system portable.

Portability constraints:

- Local-first data must be exportable.
- Domain objects must stay independent from UI framework choices.
- Android portability should be preserved, but the platform choice should happen after the family workflow is validated.
- Sync should be optional and later than trusted local use.

Domain requirement:

- The domain must not depend on storage, sync, or UI framework choices.

Exit criteria:

- Local-first data works before sync complexity is added.

## Things Not To Build First

Avoid early:

- Visual recipe editor.
- Full mobile UI.
- Exact nutrition tracker.
- Barcode scanning.
- Multi-store shopping.
- AI recipe generation.
- Complex account system.
- Detailed cost accounting.

Why:

- None of these is required to validate the core mental-load reduction loop.
