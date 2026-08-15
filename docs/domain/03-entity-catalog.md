# 03 - Entity Catalog

Generated: 2026-06-26

This catalog is domain-level. It is not a database schema.

For each entity, the test is:

> Does this reduce family cooking mental load enough to justify its existence?

## Household Context

### Family

Why it exists:

- The system must support Nasdanus now and other families later.

Why it should exist:

- It is the ownership boundary for recipes, inventory, preferences, plans, and rules.

Why not merge:

- Do not merge with user accounts. A family is a household planning unit; a user is an access/security concept.

Responsibilities:

- Own household defaults.
- Scope all family data.
- Carry locale/timezone/cultural assumptions.

### FamilyMember

Why it exists:

- Meals are planned for people, not abstract servings.

Why not merge with DiningProfile:

- A person can have changing profiles over time: age, preferences, health needs, appetite, skills.

Responsibilities:

- Represent a person.
- Link to preferences and dining profiles.

### DiningProfile

Why it exists:

- Planning needs soft constraints: appetite, age, dietary concerns, allergies, texture issues.

Why not merge with FamilyMember:

- Profiles can change without changing identity.

Responsibilities:

- Store meal-related constraints.
- Support approximate nutrition warnings.
- Support portion multipliers and preference overrides.

### Preference

Why it exists:

- Preferences can target ingredients, techniques, textures, contexts, recipes, and meal slots.

Why not store only on recipes:

- "Cora can help with mixing" or "avoid fried foods on weekdays" is not recipe-specific.

Responsibilities:

- Express like/dislike/avoid/favorite.
- Carry target type and strength.
- Allow household-level and person-level preferences.

## Kitchen Knowledge Context

### Ingredient

Why it exists:

- Ingredients are canonical food concepts used across recipes, inventory, shopping, and nutrition.

Why not merge with InventoryItem:

- `tomàquet xerri` is a concept. The tomatoes in the fridge are inventory.

Responsibilities:

- Own canonical name.
- Own aliases, forms, balance group, seasonality hints, and default units.

### IngredientAlias

Why it exists:

- The system must normalize languages, plurals, regional names, typos, and brand-like names.

Why not store aliases as a string list:

- Aliases need language, source, confidence, and review status.

Responsibilities:

- Map text to Ingredient.
- Support imports and search.

### IngredientForm

Why it exists:

- Fresh ginger and dry ginger are related but behave differently.
- Whole tomato, crushed tomato, tomato sauce, and cherry tomato are not always interchangeable.

Why not always create separate ingredients:

- Too many separate ingredients make shopping and substitution harder.

Responsibilities:

- Represent form/state when it affects use, storage, substitution, or nutrition.

### Product

Why it exists:

- Some pantry items are brand/package-specific: Oreo, Lacasitos, a specific yogurt, a jar sauce.

Why not merge with Ingredient:

- Ingredient is conceptual; Product is purchasable.

Responsibilities:

- Carry brand/package/unit information.
- Map to one or more ingredients when useful.

### Preparation

Why it exists:

- Reusable kitchen components reduce duplicated recipe maintenance.

Examples:

- Tomato sauce.
- Bechamel.
- Sofrito.
- Vinaigrette.
- Marinade.
- Cooked rice.
- Caramelised onion.
- Homemade stock.
- Dough.
- Filling.

Why not merge with Recipe:

- A preparation may be a component, a stored batch, or a technique output, not a meal.

Why not merge with Ingredient:

- It may have its own ingredients, steps, storage rules, and yield.

Responsibilities:

- Own reusable component instructions.
- Define storage behavior.
- Optionally produce prepared inventory.

### Technique

Why it exists:

- Technique affects effort, equipment, timing, and suitability.

Examples:

- Air fryer.
- Oven.
- Wok.
- Steam.
- Fry.
- Emulsify.
- Ferment.

Why not just tags:

- Techniques can require equipment and carry knowledge rules.

Responsibilities:

- Describe a cooking method.
- Link to equipment.
- Support planning and filtering.

### Equipment

Why it exists:

- Not every kitchen has the same tools.
- Some techniques require or prefer equipment.

Why not store as recipe text:

- The app must answer "what can I cook with what I have?"

Responsibilities:

- Represent available tools.
- Link to techniques and recipes.
- Carry safety/help suitability if needed.

### KitchenKnowledge

Why it exists:

- Not all knowledge belongs inside a recipe.

Examples:

- Salt aubergines before frying.
- Desalt bacallà ahead of time.
- Soak legumes.
- Refresh massa mare.
- Serve bread with saucy dishes.
- Blend onion soup for children.

Why not merge with recipes:

- The same knowledge applies to many recipes and ingredients.

Responsibilities:

- Store reusable rules, advice, safety notes, substitutions, timings, and preservation knowledge.
- Attach to ingredients, techniques, preparations, families, contexts, or equipment.

### Substitution

Why it exists:

- Mental load drops when the app can say "use this instead".

Why not just KitchenKnowledge:

- Substitution has structured direction, constraints, and consequences.

Responsibilities:

- Map source ingredient/preparation to alternative.
- Explain when it works and what changes.

## Recipe Knowledge Context

### Recipe

Why it exists:

- A complete repeatable cooking instruction deserves canonical representation.

Why not merge with MealIdea:

- A meal idea can be planned without having steps.
- A recipe should be cookable from stored data.

Responsibilities:

- Own steps, reviewed ingredients, source notes, yield, and status.
- Link to families, templates, techniques, preparations, and roles.

### RecipeIngredient

Why it exists:

- Recipe-specific ingredient use needs quantity, unit, optionality, prep note, and shopping behavior.

Why not merge with Ingredient:

- The same ingredient appears differently in each recipe.

Responsibilities:

- Preserve raw text.
- Link to ingredient or preparation.
- Store reviewed quantity/unit.

### RecipeStep

Why it exists:

- Steps, timers, and stage-specific instructions are cookable behavior.

Why not just rich text:

- Steps support timers, equipment, child-help tasks, and AI guidance.

Responsibilities:

- Store ordered instructions.
- Link optional technique, equipment, ingredient mentions, and timer.

### RecipeFamily

Why it exists:

- Many records are variants, not unrelated recipes.

Examples:

- Canelons de carbassó.
- Filet de porc.
- Iogurt bowls.
- Amanides ràpides.
- Spring rolls.

Why not just tags:

- Families have structure and member roles.

Responsibilities:

- Group variants and versions.
- Avoid duplicate maintenance.
- Support family-level planning suggestions.

### TemplateRecipe

Why it exists:

- A template captures a reusable pattern with slots.

Examples:

- Spring rolls: wrapper + protein + vegetable filling + sauce.
- Oven chicken: chicken + flavor profile + side.
- Salad: base + protein + extras + dressing.

Why not just RecipeFamily:

- Family groups records. Template defines reusable structure.

Responsibilities:

- Own slots.
- Provide defaults and allowed slot values.
- Generate or organize variants.

### TemplateSlot

Why it exists:

- Templates need explicit variable parts.

Responsibilities:

- Define slot type: protein, wrapper, sauce, topping, technique, side, timing.
- Mark required/optional.
- Link defaults.

### RecipeVariant

Why it exists:

- Variants may override parts of a template or base recipe.

Why not store as duplicate recipes only:

- Duplicates create maintenance overhead.

Responsibilities:

- Capture differences from template/base.
- Preserve variant identity.

### MealIdea

Why it exists:

- XupXup has 52 meal ideas/placeholders. They are useful, but not recipes.

Why not merge with Recipe:

- Promoting them would pollute canonical recipes and create false completeness.

Responsibilities:

- Represent low-friction meal options.
- Link to templates, contexts, and balance hints.
- Be usable in meal plans before full recipe promotion.

## Inventory Context

### InventoryItem

Why it exists:

- The family needs to know what exists at home.

Why not merge with Ingredient:

- Inventory has quantity, location, freshness, and status.

Responsibilities:

- Represent an actual stock item.
- Carry approximate or exact level.
- Link to ingredient, product, or preparation.

### PantryItem

Why it exists:

- Pantry/fridge stock needs low thresholds and recurring purchase behavior.

Why not merge with FreezerLot:

- Freezer lots need FEFO and thawing lifecycle.

Responsibilities:

- Track shelf/fridge availability.
- Trigger low-stock signals.

### FreezerLot

Why it exists:

- Frozen inventory requires lot-level tracking.

Why not generic inventory only:

- FEFO, thawing, frozen-on date, and safe-use windows are freezer-specific.

Responsibilities:

- Track frozen item, quantity, location, frozen date, best-before date, and status.
- Drive thawing recommendations.

### PreparedLot

Why it exists:

- Batch-cooked preparations and leftovers are not raw ingredients.

Why not merge with FreezerLot:

- Prepared lots can be fridge or freezer.

Responsibilities:

- Track prepared food batches.
- Link to preparation or recipe.
- Support leftovers and batch cooking.

### InventoryPolicy

Why it exists:

- Different items need different tracking intensity.

Why not store as fields everywhere:

- Policy is reusable by ingredient/product/preparation.

Responsibilities:

- Define approximate vs exact tracking.
- Define low thresholds.
- Define recurring purchase behavior.

## Planning Context

### MealPlan

Why it exists:

- Weekly planning is one of the core mental-load reducers.

Responsibilities:

- Own planning period and slots.
- Track planned, active, completed, archived status.

### MealPlanSlot

Why it exists:

- A meal plan contains concrete moments: dinner tomorrow, Saturday lunch.

Responsibilities:

- Hold recipe, meal idea, leftover, freezer lot, or free text.
- Link to context and servings.

### MealContext

Why it exists:

- Planning is not only seasonality.

Examples:

- Hot weather.
- Busy week.
- Guests.
- Picnic.
- Lunch box.
- Children at home.
- Holiday.
- Weekend batch cooking.

Why not just tags:

- Contexts influence recommendations, shopping, effort, and suitability.

Responsibilities:

- Represent situation-specific constraints and opportunities.

### ThawingPlan

Why it exists:

- Freezer usefulness depends on remembering to thaw.

Responsibilities:

- Schedule thaw start.
- Track status.
- Warn if action is needed.

### BatchCookingPlan

Why it exists:

- Batch cooking reduces future decision and effort load.

Responsibilities:

- Plan preparations or recipes made ahead.
- Link output to prepared lots/freezer lots.

### LeftoverPlan

Why it exists:

- Leftovers are inventory and future meals.

Responsibilities:

- Convert cooked food into future meal ideas or planned uses.

## Shopping Context

### ShoppingList

Why it exists:

- Shopping needs from pantry, meal planning, and manual requests must converge.

Responsibilities:

- Own shopping items and status.
- Preserve source of each need.

### ShoppingListItem

Why it exists:

- A shopping line may come from a recipe, low-stock alert, recurring purchase, or manual entry.

Responsibilities:

- Store ingredient/product/raw name, quantity, checked state, and source.

### RecurringPurchase

Why it exists:

- Some items are routine: milk, yogurt, fruit, bread.

Responsibilities:

- Create purchase needs without manual remembering.

## Balance & Nutrition Context

### NutritionSignal

Why it exists:

- The product needs approximate guidance, not exact counting.

Responsibilities:

- Store traffic-light signals.
- Explain warning reason.

### BalanceRule

Why it exists:

- Weekly balance should be explicit and configurable.

Responsibilities:

- Define target/min/max for fish, chicken, meat, eggs, legumes, vegetables, fruit, water.

### BalanceObservation

Why it exists:

- A plan or history needs measured balance against rules.

Responsibilities:

- Summarize what the week contains.
- Produce soft warnings.

## Import & Curation Context

### ImportSource

Why it exists:

- Provenance matters.

Responsibilities:

- Track DOCX, XupXup JSON, URL, paste, photo, or AI source.

### RecipeImportCandidate

Why it exists:

- Imported recipes are messy until reviewed.

Responsibilities:

- Store raw extraction.
- Hold review status.
- Link duplicate candidates.

### IngredientImportCandidate

Why it exists:

- Ingredient extraction can be uncertain.

Responsibilities:

- Hold candidate text, proposed canonical ingredient, confidence, and review decision.

### PromotionDecision

Why it exists:

- Moving from messy import to canonical data is a domain event, not a hidden script.

Responsibilities:

- Record merge, reject, promote, split, or convert-to-meal-idea decisions.

