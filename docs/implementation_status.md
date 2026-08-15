# Nasdanus - Estat d'implementacio

Actualitzat: 29 de juliol de 2026

Aquest informe complementa els informes historics d'analisi i migracio. Reflecteix l'estat real de l'aplicacio Blazor WebAssembly que hi ha al repositori.

## Resum executiu

Nasdanus ja no es nomes un receptari amb planner: ara te un flux operatiu per construir dietes equilibrades amb flexibilitat, detectant els buits que impedeixen confiar en els totals nutricionals.

Implementat:

- Receptari editable amb ingredients vinculables a ingredients coneguts.
- Base local d'ingredients amb aliases, categories, nutricio i preferencies de casa.
- Edicio manual de nutricio per ingredient conegut.
- Planner setmanal amb idees filtrables, assignacio contextual i drag/drop.
- Resum nutricional per racio/persona, no per total de recepta.
- Targeta diaria d'objectius: proteina entre 80%-110% de l'objectiu i kcal sota el limit configurat.
- Pagina `nutrition-gaps` per veure i resoldre buits de nutricio, ingredients unresolved i quantitats no utilitzables.
- Feedback/Product Backlog intern guardat dins les dades locals i exportable.

## Arquitectura actual

Runtime:

- Blazor WebAssembly static.
- MudBlazor.
- Persistencia local amb `localStorage`.
- Seed inicial JSON a `src/Nasdanus/wwwroot/data/nasdanus-seed.json`.
- Base d'ingredients local a `src/Nasdanus/wwwroot/data/ingredients.json`.
- Cap backend, cap autenticacio i cap sincronitzacio entre dispositius.

Persistencia:

- Servei central: `src/Nasdanus/Services/BrowserAppStore.cs`.
- Clau principal: `nasdanus.static.state.v1`.
- Backup automatic intern previ al save: `nasdanus.static.state.backup.v1`.
- Ultim save: `nasdanus.static.lastSavedAt.v1`.
- Export/import d'usuari: pagina `src/Nasdanus/Components/Pages/DataManagement.razor`.

## Funcionalitat implementada

### 1. Edicio de receptes i vinculacio d'ingredients

Fitxers principals:

- `src/Nasdanus/Components/Pages/RecipeEdit.razor`
- `src/Nasdanus/Services/IngredientKnowledgeService.cs`
- `src/Nasdanus/Services/RecipeService.cs`

Que fa:

- Cada ingredient de recepta es pot vincular a un ingredient conegut.
- El selector d'ingredient conegut es pot cercar per nom, traduccions i aliases.
- La categoria de recepta accepta multiples valors predefinits.
- Equipment esta separat de tags.
- Family approved es pot marcar com a estat de recepta.

Per que importa:

- Sense ingredient conegut no hi ha nutricio fiable.
- Les categories i tags alimenten el planner i les idees de recepta.

### 2. Base local d'ingredients

Fitxers principals:

- `src/Nasdanus/wwwroot/data/ingredients.json`
- `Knowledge/ingredients.json`
- `Knowledge/aliases.json`
- `Knowledge/nutrition.json`
- `src/Nasdanus.KnowledgeImporter/Data/usda-sr-legacy-mediterranean-starter.json`

Millores incorporades:

- `Oli d'oliva` com a nom primari.
- `AOVE` com a alias d'`Oli d'oliva`.
- Olives verdes farcides d'anxova.
- Mascarpone.
- Pebrots diferenciats per color/varietat, no nomes `varietat 1`, `varietat 2`.

Regla aplicada:

- Les receptes apunten a ingredients, no a productes comercials.
- Un producte comercial pot inspirar una dada manual, pero la nutricio utilitzada pel planner viu a l'ingredient conegut.

### 3. Edicio manual de nutricio

Fitxers principals:

- `src/Nasdanus/Components/Shared/IngredientNutritionDialog.razor`
- `src/Nasdanus/Components/Pages/Settings.razor`
- `src/Nasdanus/Components/Pages/NutritionGaps.razor`
- `src/Nasdanus/Services/IngredientKnowledgeService.cs`
- `src/Nasdanus/Services/BrowserAppStore.cs`
- `src/Nasdanus/Domain/Ingredient.cs`

Que fa:

- Permet informar kcal, proteina, hidrats, greix, fibra, sucre i sal per 100 g.
- Es pot editar qualsevol ingredient conegut, tingui o no nutricio previa.
- La font queda com `manual` per defecte.
- Les dades manuals es preserven quan es torna a aplicar la base de coneixement local.

Implementacio clau:

- `IngredientNutritionManualEdit` encapsula la nutricio editada.
- `IngredientKnowledgeService.SaveNutritionAsync(...)` desa la dada sobre l'ingredient conegut.
- `BrowserAppStore.ApplyKnowledge(...)` no trepitja nutricio si `NutritionSource == "manual"` i hi ha valors informats.

### 4. Planner i idees de recepta

Fitxer principal:

- `src/Nasdanus/Components/Pages/WeeklyPlanner.razor`

Que fa:

- Les idees de recepta ja no apareixen com una massa de pills sense control.
- Es poden filtrar per categoria i per cerca.
- Es pot veure una categoria i desplegar les receptes.
- Es pot assignar una idea a dia/apat amb menu contextual.
- Es pot arrossegar una recepta cap al planner.

### 5. Nutricio del planner

Fitxers principals:

- `src/Nasdanus/Services/NutritionService.cs`
- `src/Nasdanus/Components/Shared/NutritionSummary.razor`
- `src/Nasdanus/Components/Pages/WeeklyPlanner.razor`
- `src/Nasdanus/wwwroot/app.css`

Que fa:

- El resum del dia del planner es mostra per racio/persona.
- Evita el solapament entre kcal i proteina en targetes denses.
- Si falten dades, l'avisu inclou enllac a `nutrition-gaps?view=planned`.

Targeta d'objectius:

- Calcula proteina diaria per persona.
- Compara contra l'objectiu configurat a `PlanningSettings.NutritionGoals.MinimumProteinGramsPerPerson`.
- OK si esta entre 80% i 110%.
- Compara kcal contra `PlanningSettings.NutritionGoals.TargetCaloriesPerPerson`, amb 2000 kcal com valor per defecte.

### 6. Pagina de buits nutricionals

Fitxer principal:

- `src/Nasdanus/Components/Pages/NutritionGaps.razor`

Ruta:

- `/nutrition-gaps`

Vistes:

- `Pla actual`: explica els avisos de la setmana planificada.
- `Sense nutricio`: ingredients coneguts sense dades nutricionals.
- `Unresolved`: ingredients de recepta no vinculats a ingredient conegut.
- `Quantitat`: ingredients amb nutricio, pero quantitat/unitat no convertible a grams.

Enllacos directes:

- `/nutrition-gaps?view=planned`
- `/nutrition-gaps?view=missing`
- `/nutrition-gaps?view=unresolved`
- `/nutrition-gaps?view=quantity`

Accions:

- Informar nutricio manualment des de la llista.
- Obrir la recepta directament a l'ancora `#ingredients` per vincular o corregir quantitat.
- Cercar per ingredient o recepta.

Per que importa:

- Fa tracable la diferencia entre "ingredient desconegut", "ingredient conegut sense nutricio" i "quantitat no usable".
- Permet arreglar les dades que fan que el planner no pugui calcular objectius amb confianca.

### 7. Configuracio / Ingredient Knowledge

Fitxer principal:

- `src/Nasdanus/Components/Pages/Settings.razor`

Millores:

- El comptador "sense nutricio" obre `/nutrition-gaps?view=missing`.
- El comptador "unresolved recipe ingredients" obre `/nutrition-gaps?view=unresolved`.
- Cada ingredient de la llista te boto `Nutricio` per obrir l'editor manual.

### 8. Feedback i Product Backlog

Fitxers principals:

- `src/Nasdanus/Components/Layout/MainLayout.razor`
- `src/Nasdanus/Components/Shared/BacklogItemDialog.razor`
- `src/Nasdanus/Components/Pages/ProductBacklog.razor`
- `src/Nasdanus/Services/ProductBacklogService.cs`
- `src/Nasdanus/Services/BrowserAppStore.cs`

On es guarda:

- El boto flotant `Feedback` obre `BacklogItemDialog`.
- En desar, `ProductBacklogService.CreateAsync(...)` crea un `ProductBacklogItem`.
- L'item queda a `LocalAppState.ProductBacklogItems`.
- `BrowserAppStore.SaveAsync()` persisteix tot l'estat a `localStorage`, clau `nasdanus.static.state.v1`.

Que captura automaticament:

- URL actual.
- Pagina/scope.
- Versio de l'app.
- Browser information.
- Context de recepta si la URL es de recepta.
- Context de planner si la URL porta `plannedMealId`.

Com veure-ho:

- Obrir `/backlog`.
- Alla es poden filtrar feedbacks per status, type, scope i prioritat.
- Cada item es pot editar, marcar com completed, eliminar o copiar com Markdown.

Com passar-ho a Codex:

- Opcio recomanada: obrir `/backlog`, clicar la icona de copiar Markdown de l'item i enganxar-ho al xat.
- Opcio completa: anar a `/data`, exportar JSON i passar el backup; el fitxer inclou `productBacklogItems`.
- Opcio rapida: des del dialog de feedback, usar `Copy diagnostic information` i enganxar el context al xat junt amb el text del problema.

Limit important:

- El feedback no es sincronitza sol amb GitHub ni amb aquest xat.
- Si canvies de navegador/dispositiu sense export/import, el backlog queda al navegador on es va crear.

## Commits recents rellevants

- `68f4701 Improve recipe planning and ingredient linking`
- `8414e8d Adjust daily nutrition summary and ingredient catalog`
- `4f743ab Add daily goal checks and nutrition editing`
- `ef7ea41 Add nutrition gaps workflow`

## Estat pendent recomanat

- Afegir export selectiu de feedbacks/backlog a Markdown des de `/backlog`.
- Fer que `/nutrition-gaps` reutilitzi una API compartida de calcul de buits en comptes de duplicar part de la conversio de quantitats.
- Afegir tests de servei per: nutricio manual preservada, unresolved ingredients i quantity gaps.
- Afegir conversions d'unitats de productes comercials habituals quan hi hagi dades de pes per unitat.
- Decidir si el Product Backlog ha d'apareixer a la navegacio principal o quedar nomes accessible des del dialog de Feedback.
