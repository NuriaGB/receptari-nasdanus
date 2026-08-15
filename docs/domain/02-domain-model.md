# 02 - Domain Model

Generated: 2026-06-26

## North Star

Receptari dels Nasdanus exists to reduce family cooking mental load.

The domain should help the family answer:

- What can we cook without thinking too much?
- What should we use before it expires?
- What should we thaw for tomorrow?
- What should we buy?
- Is this week balanced enough?
- What meals fit today's constraints?
- What family kitchen knowledge should we not have to remember?

This is a Kitchen ERP, but "ERP" must mean orchestration and memory, not bureaucracy.

## Bounded Contexts

### 1. Household Context

Owns:

- Family
- FamilyMember
- DiningProfile
- Preference
- HouseholdEquipment

Purpose:

- Represent who the system is planning for.
- Keep preferences and constraints explicit.
- Avoid hardcoding the Nasdanus family into the product.

Mental-load reduction:

- The app remembers dislikes, age constraints, skill levels, favorite meals, and equipment availability.

### 2. Kitchen Knowledge Context

Owns:

- Ingredient
- IngredientAlias
- IngredientForm
- Preparation
- Technique
- Equipment
- KitchenKnowledge
- Substitution
- FlavorPairing

Purpose:

- Store reusable cooking concepts that should not be duplicated inside recipes.

Mental-load reduction:

- The system remembers that bacallà needs desalting, legumes may need soaking, bread goes with saucy dishes, and massa mare needs refresh cycles.

### 3. Recipe Knowledge Context

Owns:

- Recipe
- RecipeStep
- RecipeIngredient
- RecipeFamily
- TemplateRecipe
- TemplateSlot
- RecipeVariant
- MealIdea

Purpose:

- Represent repeatable meals and meal patterns.
- Keep complete recipes separate from drafts and ideas.

Mental-load reduction:

- The family can choose from full recipes, templates, variants, and lightweight ideas without re-entering everything.

### 4. Inventory Context

Owns:

- InventoryItem
- PantryItem
- FreezerLot
- PreparedLot
- InventoryPolicy
- StockLevel
- UseSoonSignal

Purpose:

- Represent what exists at home and what needs attention.

Mental-load reduction:

- The family does not have to remember what is in the freezer, what is running low, or what must be used soon.

### 5. Planning Context

Owns:

- MealPlan
- MealPlanSlot
- MealIdeaPlacement
- ThawingPlan
- BatchCookingPlan
- LeftoverPlan
- MealContext
- PlanningConstraint

Purpose:

- Turn household reality into actual meals.

Mental-load reduction:

- The system connects recipes, meal ideas, stock, freezer lots, balance, contexts, and shopping.

### 6. Shopping Context

Owns:

- ShoppingList
- ShoppingListItem
- PurchaseNeed
- RecurringPurchase

Purpose:

- Convert plans and low-stock signals into an actionable list.

Mental-load reduction:

- "Running low" and "planned meal needs this" become one consolidated list.

### 7. Balance & Nutrition Context

Owns:

- NutritionSignal
- BalanceRule
- BalanceObservation
- MealBalanceSummary

Purpose:

- Provide approximate warnings and weekly balance checks.

Mental-load reduction:

- The app says "this week is light on fish" or "this meal is rich" without demanding calorie accounting.

### 8. Import & Curation Context

Owns:

- ImportSource
- RecipeImportCandidate
- IngredientImportCandidate
- DuplicateCandidate
- PromotionDecision
- ReviewStatus

Purpose:

- Keep messy inputs out of canonical data until reviewed.

Mental-load reduction:

- The family can import now, clean later.

## Core Aggregates

### Family Aggregate

Root: `Family`

Includes:

- Family members.
- Household-level preferences.
- Household equipment.

Reason:

- Family is the boundary for data ownership and planning assumptions.

### Recipe Aggregate

Root: `Recipe`

Includes:

- Steps.
- Recipe ingredients.
- Notes.
- Source provenance.

Reason:

- A recipe should be internally consistent when promoted to canonical status.

Does not include:

- Pantry stock.
- Meal history.
- Shopping state.
- Family-specific ratings unless explicitly attached as feedback.

### Recipe Family Aggregate

Root: `RecipeFamily`

Includes:

- Family members.
- Template recipe reference.
- Variant relationships.

Reason:

- Many recipes are better understood as variants.
- This reduces duplicated maintenance and repeated decisions.

### Template Recipe Aggregate

Root: `TemplateRecipe`

Includes:

- Template slots.
- Defaults.
- Allowed substitutions or slot options.

Reason:

- A template captures reusable meal logic:
  - Spring roll wrapper + filling + sauce.
  - Oven chicken + flavor profile + side.
  - Salad base + protein + dressing.

### Preparation Aggregate

Root: `Preparation`

Includes:

- Ingredients.
- Steps.
- Storage behavior.
- Optional output lot behavior.

Reason:

- Tomato sauce, bechamel, sofrito, doughs, stock, cooked rice, and marinades are reusable objects.
- They may be cooked, stored, frozen, or used inside recipes.

### Inventory Aggregate

Root: `InventoryItem` or `FreezerLot`

Reason:

- Pantry and freezer behavior differ.
- Freezer needs FEFO, thawing, lot status, and safe-use tracking.
- Pantry often needs approximate levels and low-stock thresholds.

### Meal Plan Aggregate

Root: `MealPlan`

Includes:

- Meal slots.
- Planned recipes, ideas, leftovers, freezer lots.
- Planning context.

Reason:

- Meal planning is where the domain reduces decision fatigue most directly.

### Shopping List Aggregate

Root: `ShoppingList`

Includes:

- Shopping items.
- Source links to meal plans, low-stock signals, and manual entries.

Reason:

- Shopping should merge needs from many origins into one action list.

## Main Domain Flows

### Flow 1: Plan Tonight's Dinner

Inputs:

- Family preferences.
- Time available.
- Pantry/freezer state.
- Meal context.
- Balance signals.

Output:

- A few suitable meal options, not a huge search result.

Domain objects involved:

- MealContext
- MealIdea
- Recipe
- TemplateRecipe
- InventoryItem
- FreezerLot
- BalanceRule
- Preference

### Flow 2: Plan Tomorrow's Thawing

Inputs:

- Future meal plan.
- Freezer lots.
- Thawing rules.

Output:

- ThawingPlan actions.

Domain objects involved:

- MealPlanSlot
- FreezerLot
- ThawingPlan
- KitchenKnowledge

### Flow 3: Make Shopping List

Inputs:

- Meal plan.
- Recipe ingredients.
- Pantry low-stock signals.
- Recurring purchases.

Output:

- Consolidated shopping list.

Domain objects involved:

- ShoppingList
- PurchaseNeed
- PantryItem
- RecipeIngredient
- IngredientAlias

### Flow 4: Import A Recipe

Inputs:

- DOCX, XupXup JSON, pasted text, AI extraction.

Output:

- Review candidate.
- Suggested duplicates.
- Suggested ingredients.
- Suggested family/template.

Domain objects involved:

- ImportSource
- RecipeImportCandidate
- IngredientImportCandidate
- DuplicateCandidate
- PromotionDecision

## Object Classification Examples

### Tomato Sauce

Can be:

- Ingredient, when bought as a jar.
- Preparation, when homemade.
- Inventory item, when present in pantry/fridge/freezer.
- Recipe component, when used in pasta, pizza, canelons, or stews.

Recommended model:

- Canonical `Ingredient`: tomato sauce / salsa de tomàquet.
- `Preparation`: homemade tomato sauce.
- `InventoryItem`: actual jar or batch.
- `RecipeIngredient`: reference to either ingredient or preparation.

### Bechamel

Recommended model:

- `Preparation`.
- Optional `RecipeIngredient` component.
- Optional `PreparedLot` if batch-cooked.

Why not just recipe:

- It is a reusable component, not usually a meal.

### Sofrito

Recommended model:

- `Preparation` plus `Technique`.

Why both:

- "Make a sofrito" is a process.
- A finished sofrito can also be stored or reused.

### Vinaigrette

Recommended model:

- `Preparation`, often no inventory tracking unless made ahead.

### Marinade

Recommended model:

- `Preparation` or `KitchenKnowledge` depending on specificity.

If it has exact ingredients and is reused, make it a `Preparation`.
If it is a rule like "marinate overnight", make it `KitchenKnowledge`.

### Cooked Rice / Cooked Pasta

Recommended model:

- `Preparation` when intentionally made as a component.
- `PreparedLot` or leftover when stored.

Why it matters:

- It can answer "I have cooked rice, what can I make?"

### Caramelised Onion

Recommended model:

- `Preparation`.
- Can become freezer/fridge inventory.

### Homemade Stock

Recommended model:

- `Preparation`.
- Strong freezer-lot behavior.

### Doughs

Recommended model:

- `Preparation`.
- May have fermentation schedule and freezer suitability.
- Can belong to a `TemplateRecipe`.

### Fillings

Recommended model:

- `Preparation` if reused across dishes.
- Otherwise part of recipe steps.

## Domain Model Conclusion

The durable model is built around four questions:

1. What do we know how to cook?
2. What do we have?
3. What fits today?
4. What must we remember so the family does not have to?

Everything else is secondary.

