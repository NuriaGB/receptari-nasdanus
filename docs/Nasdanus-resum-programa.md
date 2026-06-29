# Nasdanus - Resum del programa

Data del resum: 29 de juny de 2026

## 1. Visio general

Nasdanus es una aplicacio familiar per planificar menjars, gestionar receptes, cuinar pas a pas i generar una llista de la compra setmanal.

L'objectiu actual no es automatitzar decisions, sino reduir carrega mental: recordar que toca cuinar, que cal preparar, que cal comprar i quines idees de receptes estan pendents d'assignar.

El producte esta orientat a un us mobile-first per al dia a dia:

- Mobil: Avui, Planner, Cooking Mode, Compra i Rebost.
- Escriptori/web: edicio de receptes, entrada de text llarga i gestio mes comoda del receptari.

## 2. Estat actual

L'aplicacio ja te una vertical slice funcional:

- Home / Avui
- Receptari
- Detall de recepta
- Edicio de recepta
- Cooking Mode
- Planner setmanal
- Llista de la compra
- Rebost basic
- Dades, backup i restauracio
- Product Backlog intern
- PWA / desplegament static a GitHub Pages

El seed actual inclou:

- 131 receptes importades.
- 125 receptes en estat draft.
- 6 receptes consolidades.
- 8 ingredients inicials de rebost.

Nota: com que l'app funciona amb estat local del navegador, aquestes xifres descriuen el seed inicial. Despres d'usar l'aplicacio, cada navegador pot tenir dades diferents a `localStorage`.

## 3. Tecnologia

Stack actual:

- Blazor WebAssembly
- .NET `net10.0`
- MudBlazor
- PWA static
- GitHub Pages
- Persistencia local amb `localStorage`
- Seed inicial en JSON: `wwwroot/data/nasdanus-seed.json`

No hi ha:

- Autenticacio
- Comptes d'usuari
- Backend extern
- Azure
- IA
- Sincronitzacio entre dispositius
- SQLite/EF Core en runtime static

Inicialment el producte apuntava a SQLite i EF Core, pero per preparar el desplegament a GitHub Pages es va convertir a una arquitectura estatica Blazor WebAssembly. Les dades es carreguen del seed JSON i els canvis es desen al navegador.

## 4. Navegacio i UX general

La navegacio principal inclou:

- Avui
- Receptes
- Planner
- Compra
- Rebost
- Dades

En desktop es mostra a la barra superior. En mobil es mostra com a navegacio inferior fixa.

El color primari de l'aplicacio es:

`#3891a6`

La UI segueix una linia simple i practica, amb prioritat per:

- Targetes compactes.
- Botons grans quan l'us es mobil o de cuina.
- Evitar pantalles massa denses en mobil.
- Mantenir edicio de receptes comoda en desktop.

## 5. Home / Avui

La pagina d'inici mostra el que cal cuinar avui:

- Dinar d'avui.
- Sopar d'avui.
- Receptes planificades per cada apat.
- Boto per obrir la recepta.
- Boto per obrir el planner.

Si no hi ha cap recepta assignada, la targeta queda mes compacta i mostra "Sense recepta assignada" com a estat buit.

També inclou placeholders MVP:

- "Preparar dema" amb recordatoris mock.
- Resum setmanal mock: peix, llegums, verdures i proteina.
- Recordatoris futurs mock, pensats per preparacio, congelador i objectius setmanals.

Encara no hi ha logica real d'intelligencia, nutricio o recordatoris calculats.

## 6. Receptari

El receptari permet:

- Veure totes les receptes.
- Cercar per nom.
- Filtrar per categoria.
- Obrir una recepta.
- Crear una recepta rapida.
- Veure si una recepta es draft.
- Marcar una recepta draft com a consolidada.
- Veure si una recepta es una variacio.

### Quick Add Recipe

La creacio rapida permet guardar una idea sense omplir tots els camps.

Camps disponibles:

- Nom, obligatori.
- Descripcio curta.
- Categoria.
- Ingredients en text lliure.
- Passos en text lliure.
- Temps de preparacio.
- Temps de coccio.
- Racions.

Regla de negoci aplicada:

> Es millor guardar una recepta incompleta que perdre la idea.

Les receptes incompletes es desen com a `Draft`, apareixen al receptari i es poden assignar al planner.

## 7. Consolidacio i variacions

Les receptes poden tenir estat:

- `Draft`
- `Active`

Una recepta draft es pot marcar com a consolidada manualment. Aquesta decisio no depen estrictament de tenir tots els camps plens; es una decisio de l'usuari.

També es pot crear una variacio:

- "Fer variacio" crea una copia editable de la recepta.
- La nova recepta queda en estat `Draft`.
- La variacio queda vinculada a la recepta original amb `VariationOfRecipeId`.
- La variacio es pot editar sense modificar la recepta original.

Aquest enfocament permet mantenir una recepta base i provar alternatives sense perdre la versio consolidada.

## 8. Detall de recepta

El detall de recepta mostra:

- Nom.
- Descripcio.
- Categoria.
- Estat draft, si escau.
- Indicador de variacio, si escau.
- Recepta original, si es una variacio.
- Temps de preparacio.
- Temps de coccio.
- Dificultat.
- Racions base.
- Ingredients.
- Passos.
- Notes.

Accions disponibles:

- Cuinar.
- Editar.
- Fer variacio.
- Consolidar, si la recepta es draft.

### Escalat de racions

Cada recepta te unes racions base. Quan s'obre fora del planner, l'usuari pot ajustar temporalment el nombre de racions.

Les quantitats dels ingredients es recalculen amb:

`racions_mostrades / racions_base`

L'escalat respecta el mode de cada ingredient:

- `linear`
- `fixed`
- `approximate`
- `to_taste`

## 9. Edicio de receptes

L'edicio de receptes permet modificar:

- Nom.
- Descripcio.
- Categoria.
- Racions.
- Temps de preparacio.
- Temps de coccio.
- Dificultat.

Ingredients:

- Afegir.
- Editar nom.
- Editar quantitat.
- Editar unitat.
- Editar mode d'escalat.
- Reordenar.
- Eliminar.

Passos:

- Afegir.
- Editar titol.
- Editar descripcio.
- Editar timer.
- Reordenar.
- Eliminar.
- Assignar ingredients utilitzats en cada pas.

Notes:

- General Notes.
- Cooking Tips.
- Variations.

El model ja esta preparat per futures capacitats com:

- Favorits.
- Rating.
- Historial de cuina.
- Tags.
- Recomanacio estacional.

Aquestes funcionalitats futures encara no tenen UI completa.

## 10. Cooking Mode

Cooking Mode es la vista prioritaria per cuinar al mobil.

Funcionalitats:

- Mostra un sol pas cada vegada.
- No mostra tota la recepta sencera.
- Boto Anterior.
- Boto Seguent.
- Boto Completar.
- En completar un pas, avanca automaticament al seguent.
- En l'ultim pas mostra "Finalitzar recepta".
- Indicador de progres.
- Timers integrats quan un pas en defineix un.
- Botons grans i tactils.
- Confirmacio si s'intenta sortir d'una sessio no finalitzada.

Cada pas pot tenir ingredients propis vinculats pel model:

- Ingredient.
- Quantitat.
- Unitat.

Cooking Mode mostra aquests ingredients directament sota la descripcio del pas.

Les quantitats dels ingredients del pas tambe s'escalen segons les racions planificades o temporals.

## 11. Planner setmanal

El planner esta orientat a apats, no a una taula de calendari.

Caracteristiques:

- Navegacio per setmanes.
- Setmana anterior.
- Setmana seguent.
- Planificacio de qualsevol setmana futura.
- Set dies com a targetes independents.
- Layout responsive:
  - Mobil: una targeta de dia per fila.
  - Tablet: dues o tres targetes per fila.
  - Desktop: tantes com hi cabin comodament.

Cada dia conte:

- Nom del dia i data.
- Dinar.
- Sopar.

Cada apat permet:

- Afegir diversos plats.
- Mostrar receptes com files compactes.
- Veure racions planificades.
- Obrir la recepta.
- Eliminar una recepta de l'apat.

Cada entrada planificada desa:

- Recepta.
- Apat.
- Data.
- Ordre.
- Racions planificades.

Important: les racions planificades es desen a l'entrada del planner, no a la recepta base.

## 12. Selector de receptes del planner

El selector permet:

- Cercar receptes per nom.
- Filtrar per categoria.
- Filtrar per tipus d'apat.
- Ordenar alfabeticament.
- Seleccionar una recepta existent.
- Crear una recepta draft rapida.
- Definir racions planificades.

No hi ha drag and drop encara.

## 13. Recipe Ideas

El planner inclou una seccio "Recipe Ideas".

Objectiu:

- Guardar visualment plats que podrien cuinar-se durant la setmana.
- Ajudar a decidir que cuinar abans d'assignar-ho a un dia concret.

Actualment:

- Es mostra com a xips compactes.
- Inclou receptes no assignades a la setmana visible.
- Es poden descartar visualment de la llista.
- Les idees descartades es desen per setmana i formen part del backup.

Limitacio actual:

- Recipe Ideas encara no es una safata editorial completa; continua sent una ajuda lleugera dins del planner setmanal.

## 14. Llista de la compra

La llista de la compra es genera a partir del planner setmanal.

Funcionalitats:

- Seleccionar setmana.
- Generar/regenerar la llista.
- Analitzar tots els apats planificats.
- Respectar racions planificades.
- Escalar quantitats.
- Fusionar ingredients duplicats.
- Agrupar per categoria.
- Mostrar seccions verticals pensades per supermercat.
- Mostrar total d'items.
- Mostrar items comprats.
- Barra de progres.
- Marcar item com a comprat.
- Mantenir items comprats visibles pero atenuats.
- Marcar tot com a comprat.
- Netejar items comprats.
- Afegir items manuals.
- Editar items.
- Eliminar items.

Categories:

- Vegetables.
- Meat.
- Fish.
- Dairy & Eggs.
- Pantry.
- Spices.
- Other.

### Fusio d'ingredients

Si el mateix ingredient apareix en diverses receptes:

- Si la unitat coincideix, suma quantitats.
- Si la unitat no coincideix, manté valors visibles separats fins que hi hagi conversio d'unitats.

També mostra origen:

- Nombre de receptes.
- Noms de receptes quan son una o dues.

## 15. Rebost

El modul de rebost representa ingredients que normalment ja hi ha a casa.

No es un inventari.

Pagina:

- Titol: "Sempre tinc".
- Explica que aquests ingredients s'exclouen de la generacio automatica de la compra.

Permet:

- Afegir ingredient.
- Editar ingredient.
- Eliminar ingredient.
- Categoritzar ingredient.

Quan es genera la llista de la compra:

- Si un ingredient coincideix amb el rebost, no s'inclou automaticament.
- L'usuari sempre pot afegir-lo manualment si aquell cop cal comprar-lo.

No hi ha:

- Quantitats d'estoc.
- Caducitats.
- Inventari real.

## 16. Freezer

Hi ha fonament de model per a un futur modul de congelador.

El model esta preparat per:

- Ingredient.
- Quantitat.
- Unitat.
- Data de congelacio.
- Consum preferent.
- Notes.

Encara no hi ha UI de congelador.

## 17. Dades i persistencia

L'estat de l'app es gestiona amb `BrowserAppStore`.

Flux:

1. En primer carregament, l'app llegeix `nasdanus-seed.json`.
2. Els canvis de l'usuari es desen a `localStorage`.
3. Les seguents sessions del mateix navegador carreguen l'estat local.

Clau de `localStorage`:

`nasdanus.static.state.v1`

Per seguretat, abans de cada desat tambe es conserva una copia interna de l'estat anterior:

`nasdanus.static.state.backup.v1`

L'ultim desat queda registrat a:

`nasdanus.static.lastSavedAt.v1`

La pagina "Dades" permet:

- Exportar totes les dades a un fitxer JSON.
- Importar una copia exportada anteriorment.
- Validar el fitxer abans de substituir dades locals.
- Crear una copia abans d'importar.
- Veure un resum de receptes, planner, compra, rebost i Recipe Ideas.
- Incloure Product Backlog en export/import.

## 18. Product Backlog intern

Nasdanus inclou un modul intern de Product Backlog per capturar bugs, idees i observacions mentre s'utilitza l'app.

Objectiu:

- Registrar feedback en menys de 30 segons.
- Minimitzar escriptura.
- Capturar automaticament context tecnic i funcional.
- No dependre de notes externes.

Entrada rapida:

- Boto flotant global "Feedback".
- Dialeg amb cinc tipus d'entrada: Bug, Improvement, Idea, Question i Task.
- Scope, title, description, priority, status, labels i target version.

Context automatic:

- Versio de l'app.
- Pagina actual.
- URL actual.
- Data i hora.
- Browser information.
- Recepta, si la pagina permet deduir-la.
- Context de planner quan ve d'un apat planificat.
- Setmana de compra en Shopping.

Pagina dedicada:

- `Product Backlog`
- Cerca.
- Filtres.
- Ordenacio.
- Edicio.
- Eliminacio.
- Marcar com completat.
- Copia a Markdown.

Aquest modul es per desenvolupament intern del producte i no esta pensat com a funcionalitat final per usuaris externs.

Aquest enfocament permet GitHub Pages i us offline/PWA basic, pero implica:

- Les dades no se sincronitzen entre dispositius.
- Esborrar dades del navegador pot eliminar canvis locals.
- Per tenir multi-dispositiu caldra una capa de sync futura.

L'arquitectura esta separada en:

- Models de domini.
- Serveis d'aplicacio.
- Components de pagina.
- Components reutilitzables.
- Seed JSON.

## 19. Desplegament

El projecte esta preparat per GitHub Pages.

Configuracio:

- Blazor WebAssembly static.
- Base path: `/receptari-nasdanus/`.
- `StaticWebAssetBasePath`: `receptari-nasdanus`.
- `404.html` generat a partir d'`index.html` per suportar routing client-side.
- `.nojekyll`.
- Service worker PWA.
- Workflow GitHub Actions.

Workflow:

`.github/workflows/deploy-pages.yml`

S'executa:

- En push a `main`.
- Manualment amb `workflow_dispatch`.

Comanda principal:

```powershell
dotnet publish src/Nasdanus/Nasdanus.csproj -c Release -o publish
```

## 20. Execucio local

Per provar localment:

```powershell
dotnet run --project src\Nasdanus\Nasdanus.csproj --urls http://localhost:5088
```

URL prevista:

`http://localhost:5088/receptari-nasdanus/`

## 21. Decisions arquitectoniques importants

### Mobile-first

Les pantalles d'us diari prioritzen el mobil:

- Home.
- Planner.
- Cooking Mode.
- Shopping.
- Pantry.

L'edicio de receptes es manté comoda en desktop perquè implica text llarg, camps repetits i reordenacio.

### Receptes incompletes

Les receptes poden existir com a draft. Això permet capturar idees ràpidament sense forçar l'usuari a completar-ho tot.

### Racions planificades

Les racions d'una recepta base no es modifiquen quan es planifica un apat.

Cada `MealPlanRecipe` desa les seves racions planificades.

### Escalat d'ingredients

L'escalat es calcula en visualitzacio i generacio de compra. No modifica les quantitats base.

### Ingredients per pas

Els passos tenen referencies estructurades a ingredients, quantitat i unitat. No es depen nomes del text lliure.

### Rebost com a "sempre tinc"

El rebost no intenta gestionar stock. Només indica que un ingredient no hauria d'apareixer automaticament a la compra.

### Static-first

Per facilitar GitHub Pages, l'app actual evita backend. Aixo redueix complexitat de desplegament, pero deixa la sync com a feina futura.

## 22. Limitacions conegudes

Encara no hi ha:

- Sincronitzacio entre dispositius.
- Comptes d'usuari.
- Backend.
- Importacio de Word/PDF/foto.
- Conversio d'unitats.
- Inventari real de rebost.
- UI de congelador.
- Compra amb deduccio de congelador.
- Nutricio.
- Objectius setmanals calculats de forma real.
- Recordatoris reals.
- Historial de cuina funcional a UI.
- Tags/favorits/rating amb UI completa.
- Tests automatitzats dedicats.

Algunes parts son encara heuristiques o placeholders:

- Categoritzacio d'ingredients a la compra.
- Recordatoris de Home.
- Resum setmanal de Home.
- Recipe Ideas com a ajuda setmanal lleugera, no com a inbox completa.

## 23. Properes passes recomanades

Prioritat alta:

- Afegir tests de serveis per planner, shopping i escalat.
- Revisar textos amb accents i encoding.
- Provar export/import amb dades reals abans d'usar-ho com a sistema principal.

Prioritat mitjana:

- Millorar unitats i conversions.
- Afegir UI de tags/favorits/rating.
- Fer historial de cuina.
- Preparar sincronitzacio futura amb backend opcional.

Prioritat futura:

- Importacio de documents.
- Freezer UI.
- Pantry amb stock, només si realment aporta valor.
- Recordatoris intelligents.
- Objectius nutricionals o setmanals calculats.

## 24. Resum curt

Nasdanus ja permet:

- Guardar receptes, incloses receptes incompletes.
- Consolidar drafts.
- Crear variacions de receptes.
- Editar receptes amb ingredients, passos, timers, notes i ingredients per pas.
- Planificar dinars i sopars setmanals amb multiples receptes per apat.
- Definir racions per cada apat planificat.
- Cuinar pas a pas amb quantitats escalades.
- Generar una llista de la compra setmanal.
- Excloure ingredients habituals gracies al rebost "Sempre tinc".
- Exportar i restaurar totes les dades locals amb validacio previa.
- Capturar feedback intern amb Product Backlog i exportar-lo amb la resta de dades.
- Funcionar com a Blazor WebAssembly/PWA estatica preparada per GitHub Pages.

El producte esta en bon estat per us personal i familiar setmanal, amb limitacions conscients al voltant de sincronitzacio, imports, inventari i automatitzacions avançades.
