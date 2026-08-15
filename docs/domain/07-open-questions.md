# 07 - Open Questions

Generated: 2026-06-26

These questions should be answered before implementation decisions harden.

They are grouped by decision risk.

## Highest Priority

### 1. What is the first mental-load loop to validate?

Options:

- Weekly planning -> shopping list -> thawing reminders.
- Pantry/freezer inventory -> meal suggestions.
- Recipe import -> review -> meal planning.

Recommendation:

- Start with weekly planning, shopping, and thawing because they directly reduce recurring household load.

### 2. How much pantry precision is acceptable?

Questions:

- Should pantry items use exact quantities, approximate levels, or both?
- Which items truly need exact tracking?
- Which items should only have "running low"?

Recommendation:

- Approximate by default.
- Exact for freezer lots, expensive/perishable items, and batch-cooked lots.

### 3. What makes a XupXup placeholder useful?

Questions:

- Should all 52 placeholders become MealIdeas?
- Should some be archived?
- Which should become TemplateRecipe variants?

Recommendation:

- Import all as MealIdeas first.
- Promote only after real usage or review.

### 4. Who are the family members and what constraints matter?

Questions:

- Which preferences belong to the whole family?
- Which belong to specific people?
- Are age, birth date, cooking skill, or child-help suitability needed now?

Recommendation:

- Start with names, dislikes, allergies/intolerances if any, favorite meals, and child-help notes.
- Defer sensitive health tracking unless clearly useful.

### 5. What is the minimum weekly balance model?

Questions:

- Exact targets or soft warnings?
- Which categories matter now?

Recommendation:

- Start with soft weekly signals for fish, chicken, meat, eggs, legumes, vegetables, fruit, and water.

## High Priority

### 6. How should Recipe, MealIdea, and TemplateRecipe be explained to users?

Risk:

- If these concepts feel technical, they will increase mental load.

Recommendation:

- Domain distinction internally.
- User-facing language can be simpler:
  - Recipe.
  - Idea.
  - Family/template as "versions" or "ways to make".

### 7. Which preparations deserve promotion first?

Candidates:

- Bechamel.
- Salsa de tomàquet.
- Sofregit.
- Massa mare.
- Brou/fumet.
- Marinades.
- Cooked rice.

Recommendation:

- Promote preparations that affect planning, shopping, freezing, or repeated recipes.

### 8. How should equipment availability be captured?

Questions:

- Does the family have air fryer, vaporera, wok, pressure cooker, Thermomix, BBQ?
- Should recipes be filtered by available equipment?

Recommendation:

- Capture equipment as household capabilities.
- Link techniques to equipment.

### 9. What contexts matter most?

Candidates:

- Busy weekday.
- Hot weather.
- Weekend cooking.
- Guests.
- Children at home.
- Lunch box.
- Picnic.
- Holidays.

Recommendation:

- Start with 5-8 contexts that actually drive meal choices.

### 10. How should leftovers be handled?

Questions:

- Are leftovers planned intentionally?
- Should leftover lots be tracked like prepared inventory?
- Should recipes suggest transformations?

Recommendation:

- Add simple PreparedLot/LeftoverPlan early if freezer/batch cooking is important.

## Medium Priority

### 11. How much nutrition data is enough?

Questions:

- Should recipes have manual green/orange/red signals?
- Should kcal ranges be estimated later?

Recommendation:

- Start with manual/heuristic traffic lights and weekly balance.
- Defer exact kcal sources.

### 12. Should products and brands be modeled now?

Examples:

- Oreo.
- Lacasitos.
- Yogurt brands.

Recommendation:

- Use Product only where shopping needs brand/package identity.
- Do not over-normalize brand products.

### 13. What is the review workflow?

Questions:

- Who reviews imported recipes?
- What does "good enough" mean?
- Can recipes be usable while partially reviewed?

Recommendation:

- Allow partially reviewed recipes for browsing.
- Require ingredient review before shopping automation.

### 14. What data should AI be allowed to change?

Recommendation:

- AI can create candidates and suggestions.
- Human review is required for canonical data.

### 15. Should meal history be automatic or manual?

Options:

- Auto-create history from completed meal plans.
- Manual "we ate this" action.
- Both.

Recommendation:

- Start with manual confirmation from meal plan slots.

## Lower Priority

### 16. Do we need cost tracking?

Recommendation:

- Defer.

### 17. Do we need multi-store shopping?

Recommendation:

- Defer.

### 18. Do we need barcode scanning?

Recommendation:

- Defer until Product modeling proves valuable.

### 19. Do we need full sync/multi-user conflict handling?

Recommendation:

- Defer until local-first workflows are validated.

### 20. Do we need exact per-person portions?

Recommendation:

- Defer. Use serving estimates and portion multipliers first.

## Data Review Questions From Current Artifacts

### DOCX

- Is `Quiche de Pastanaga` contaminated with salmon steps?
- Is `Spanakopita` description accidentally copied from `Sopa de Ceba`?
- Should `Fricandó amb moixernons *` keep or remove the asterisk?
- Which `Coca de Sant Joan (versió millorada)` copy should survive?

### XupXup

- Which 52 placeholders are real meal ideas?
- Which should be archived?
- Are `gingebre fresc` and `gingebre sec` separate ingredient forms or separate ingredients?
- Should `Pollastre rostit` placeholder merge with DOCX `Pollastre Rostit` or remain a meal idea linked to the family?
- Should `Croquetes de Pollastre rostit` be linked as leftover transformation?

### Families/Templates

- Should `Rotllets de primavera` become a template now, even with only one draft variant?
- Are yogurt bowls worth modeling as a template or only as recurring snacks?
- Should salads be full recipes or meal ideas with slots?
- Should `Llenties amb verduretes` be a template for legume dinners?

## Final Open Question

When the app suggests a meal, what should feel like success?

Possible answer:

> The family spends less time negotiating, remembering, and checking, and more time simply cooking and eating something good enough.

That answer should guide all future design.

