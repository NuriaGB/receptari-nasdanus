using Microsoft.EntityFrameworkCore;
using Nasdanus.Domain;

namespace Nasdanus.Data;

public sealed class DatabaseInitializer(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task InitializeAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Recipes.AnyAsync())
        {
            var recipes = SeedRecipes();
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        }

        await EnsureCurrentWeekSeedAsync(db);
    }

    private static async Task EnsureCurrentWeekSeedAsync(NasdanusDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = WeekStart(today);

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = weekStart.AddDays(dayOffset);
            foreach (var mealKind in new[] { MealKind.Lunch, MealKind.Dinner })
            {
                var exists = await db.MealPlanSlots.AnyAsync(slot => slot.Date == date && slot.MealKind == mealKind);
                if (!exists)
                {
                    db.MealPlanSlots.Add(new MealPlanSlot
                    {
                        Date = date,
                        MealKind = mealKind
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        var lunch = await db.MealPlanSlots.FirstAsync(slot => slot.Date == today && slot.MealKind == MealKind.Lunch);
        var dinner = await db.MealPlanSlots.FirstAsync(slot => slot.Date == today && slot.MealKind == MealKind.Dinner);

        lunch.RecipeId ??= await db.Recipes
            .Where(recipe => recipe.Name == "Samuses de vedella amb verduretes a l'airfryer")
            .Select(recipe => recipe.Id)
            .FirstAsync();

        dinner.RecipeId ??= await db.Recipes
            .Where(recipe => recipe.Name == "Cassoleta de verdures, pollastre i patata bullida")
            .Select(recipe => recipe.Id)
            .FirstAsync();

        await db.SaveChangesAsync();
    }

    private static DateOnly WeekStart(DateOnly date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - date.DayOfWeek;
        return date.AddDays(offset);
    }

    private static List<Recipe> SeedRecipes() =>
    [
        CreateRecipe(
            "Samuses de vedella amb verduretes a l'airfryer",
            "Samuses cruixents amb vedella, verduretes i espècies, pensades per a un dinar amb poc esforç actiu.",
            "Dinar",
            20,
            15,
            2,
            2,
            [
                Ingredient("vedella", "300", "g"),
                Ingredient("pastanaga", "1", "unitat"),
                Ingredient("carbassó", "1/2", "unitat"),
                Ingredient("gingebre fresc", "1", "trosset"),
                Ingredient("garam masala", "1", "culleradeta"),
                Ingredient("cúrcuma", "1/2", "culleradeta"),
                Ingredient("pebre negre"),
                Ingredient("sal"),
                Ingredient("salsa de soja", "1", "culleradeta"),
                Ingredient("jalapeños", "1", "culleradeta"),
                Ingredient("llavors de sèsam"),
                Ingredient("oli d'oliva")
            ],
            [
                Step("Talla els ingredients", "Talla la vedella a tires fines tipus wok. Talla la pastanaga i el carbassó a juliana fina. Pica molt el gingebre."),
                Step("Salteja la vedella", "En una paella gran amb oli d'oliva, salteja la vedella salpebrada a foc alt durant 1-2 minuts.", 2),
                Step("Afegeix les verdures", "Afegeix la pastanaga, el carbassó i el gingebre. Cuina 2-3 minuts perquè quedin amb textura.", 3),
                Step("Condimenta", "Afegeix garam masala, cúrcuma, jalapeño i salsa de soja. Remena 1 minut sense coure massa.", 1),
                Step("Refreda el farcit", "Afegeix suc de llimona o taronja i deixa refredar 5 minuts abans d'embolicar.", 5),
                Step("Embolica", "Mulla l'oblea amb aigua calenta 10-15 segons, posa-hi farcit i plega-la en triangle o rotllet."),
                Step("Cou a l'airfryer", "Pinzella amb oli, afegeix sèsam si vols i cou a 195 °C fins que sigui daurat.", 8),
                Step("Prepara la salsa", "Barreja iogurt, llimona, un pessic de garam masala i sal.")
            ]),
        CreateRecipe(
            "Blat sarraí cremós amb pit de pollastre i verduretes",
            "Plat cremós de blat sarraí amb pollastre, carbassó, pastanaga i un toc de llimona.",
            "Dinar",
            10,
            20,
            2,
            2,
            [
                Ingredient("blat sarraí", "120", "g"),
                Ingredient("pit de pollastre", "1", "unitat"),
                Ingredient("carbassó", "1/2", "unitat"),
                Ingredient("pastanaga mitjana", "1", "unitat"),
                Ingredient("gingebre fresc ratllat", "1", "culleradeta"),
                Ingredient("cúrcuma", "1", "culleradeta"),
                Ingredient("pebre negre", "1", "pessic"),
                Ingredient("oli d'oliva verge extra", "1", "cullerada"),
                Ingredient("brou de pollastre o verdures", "450", "ml"),
                Ingredient("suc de llimona", "1/4", "unitat"),
                Ingredient("sal", "1", "pessic")
            ],
            [
                Step("Renta el blat", "Renta el blat sarraí sota l'aixeta en un colador durant uns segons."),
                Step("Aromatitza l'oli", "En una cassola, aromatitza l'oli amb el gingebre ratllat durant 30-40 segons."),
                Step("Cuina les verdures", "Afegeix la pastanaga i el carbassó a daus petits. Cuina 5 minuts.", 5),
                Step("Torra el blat", "Afegeix la cúrcuma i el pebre negre. Incorpora el blat sarraí i remena 1 minut.", 1),
                Step("Afegeix el brou", "Afegeix el brou calent i deixa coure fins que quedi tendre i cremós.", 12),
                Step("Cou el pollastre", "Talla el pit de pollastre a daus petits i posa'ls al cremós els últims 5-6 minuts."),
                Step("Acaba", "Apaga el foc, afegeix suc de llimona, julivert picat i un raig d'oli. Remena i deixa reposar.", 2),
                Step("Serveix", "Torra unes pipes de carbassa i posa-les per sobre per donar un toc cruixent.")
            ]),
        CreateRecipe(
            "Fals rissotto amb gambes i musclos",
            "Arròs cremós amb brou ràpid de gamba, musclos, verduretes i llimona.",
            "Dinar",
            15,
            25,
            3,
            2,
            [
                Ingredient("arròs", "140", "g"),
                Ingredient("gamba", "12", "u"),
                Ingredient("cloïssa", "2", "grapats"),
                Ingredient("pastanaga", "1", "unitat"),
                Ingredient("carbassó", "1/2", "unitat"),
                Ingredient("espinacs", "1", "grapat"),
                Ingredient("gingebre fresc", "1", "tros"),
                Ingredient("cúrcuma", "1/2", "culleradeta"),
                Ingredient("pebre negre"),
                Ingredient("oli d'oliva"),
                Ingredient("llimona")
            ],
            [
                Step("Fes el brou", "En una cassola amb oli, sofregeix els caps i closques de les gambes 2-3 minuts. Afegeix aigua i el líquid dels musclos, i bull.", 10),
                Step("Marca les gambes", "En una paella amb oli, salta lleugerament les gambes salpebrades.", 1),
                Step("Fes la base vegetal", "Sofregeix la pastanaga 3 minuts. Afegeix carbassó i gingebre ratllat, i sofregeix 2 minuts més.", 5),
                Step("Torra l'arròs", "Afegeix l'arròs rentat, remena 1-2 minuts, afegeix cúrcuma i pebre, i remena 1 minut.", 4),
                Step("Cou amb brou", "Afegeix el brou calent a poc a poc i deixa coure 10-12 minuts."),
                Step("Afegeix el marisc", "Quan faltin 3 minuts, afegeix les gambes. Quan falti 1 minut, afegeix els musclos."),
                Step("Reposa", "Retira del foc, afegeix oli cru, suc de llimona i pebre negre. Deixa reposar 2 minuts.")
            ]),
        CreateRecipe(
            "Cassoleta de verdures, pollastre i patata bullida",
            "Safata de pollastre, patata i verdures amb marinada cítrica i cúrcuma.",
            "Sopar",
            20,
            35,
            2,
            2,
            [
                Ingredient("pit de pollastre", "1", "unitat"),
                Ingredient("patata", "2", "unitats"),
                Ingredient("pastanaga", "2", "unitats"),
                Ingredient("carbassó", "1/2", "unitat"),
                Ingredient("albergínia", "1/2", "unitat"),
                Ingredient("oli d'oliva"),
                Ingredient("cúrcuma"),
                Ingredient("taronja"),
                Ingredient("salsa de soja", "1", "cullerada"),
                Ingredient("pebre negre"),
                Ingredient("sal"),
                Ingredient("vinagre")
            ],
            [
                Step("Marina el pollastre", "Talla el pollastre a daus grans i barreja'l amb oli, cúrcuma, gingebre, suc de taronja, soja, pebre i sal.", 30),
                Step("Bull les patates", "Bull les patates amb pell en aigua i sal fins que quedin fermes. Refreda-les, talla-les i deixa-les assecar.", 30),
                Step("Prepara la pastanaga", "Talla les pastanagues a rodanxes gruixudes o bastons. Bull-les 5 minuts si vols textura més fina.", 5),
                Step("Comença la safata", "Barreja patates, pastanagues i pollastre sense tot el líquid. Afegeix una mica de marinada, oli i pebre. Forn a 210 °C.", 15),
                Step("Prepara les verdures", "Talla el carbassó i l'albergínia a daus grans. Sala lleugerament, espera, asseca i barreja amb oli i pebre.", 10),
                Step("Acaba al forn", "Afegeix carbassó i albergínia a la safata. Cou fins que el pollastre sigui daurat i la verdura mantingui textura.", 15),
                Step("Redueix la marinada", "Bull la marinada restant en un cassó i barreja-la amb oli d'oliva i vinagre de poma.", 4),
                Step("Reposa", "Quan surti del forn, afegeix vinagreta, julivert i ratlladura de taronja. Remena suaument.", 3)
            ]),
        CreateRecipe(
            "Pa de Pita integral",
            "Pa pla integral per omplir o acompanyar plats amb salsa.",
            "Pa i masses",
            20,
            7,
            2,
            4,
            [
                Ingredient("farina de blat integral", "200", "g"),
                Ingredient("farina", "50", "g"),
                Ingredient("llevat", "12", "g"),
                Ingredient("oli d'oliva", "1", "cullerada"),
                Ingredient("sal", "1", "culleradeta"),
                Ingredient("mel", "1/2", "culleradeta"),
                Ingredient("llavors de sèsam", "1", "cullerada")
            ],
            [
                Step("Activa el llevat", "Barreja aigua tèbia, llevat i mel. Deixa reposar.", 5),
                Step("Pasta", "Incorpora les farines, la sal, l'oli i el sèsam. Pasta amb suavitat.", 10),
                Step("Fermenta", "Tapa amb un drap i deixa fermentar, fent un plec a mitja fermentació.", 60),
                Step("Enforna", "Amb el forn molt calent a 250 °C, cou sobre una safata o pedra calenta durant 5-7 minuts.")
            ]),
        CreateRecipe(
            "Plumcake de taronja tendre",
            "Plumcake de taronja amb gotes de xocolata i glassejat suau.",
            "Postres",
            15,
            40,
            1,
            8,
            [
                Ingredient("ous", "2", "unitats"),
                Ingredient("panela", "60", "g"),
                Ingredient("sucre blanc", "30", "g"),
                Ingredient("mel", "40", "g"),
                Ingredient("oli d'oliva suau", "90", "ml"),
                Ingredient("farina de blat", "120", "g"),
                Ingredient("llevadura química", "6", "g"),
                Ingredient("ratlladura de taronja", "1", "taronja"),
                Ingredient("suc de taronja", "80", "ml"),
                Ingredient("sal", "1", "pessic"),
                Ingredient("gotes de xocolata", "1/2", "tassa"),
                Ingredient("sucre de llustre", "3", "cullerades")
            ],
            [
                Step("Escalfa el forn", "Escalfa el forn a 170 °C."),
                Step("Bat els ous", "Afegeix ous, panela, sucre blanc i mel en un bol. Bat fins que blanquegi."),
                Step("Afegeix líquids", "Afegeix l'oli, la ratlladura i el suc de taronja. Bat fins incorporar."),
                Step("Tamisa", "Tamisa la farina, la llevadura i la sal."),
                Step("Integra", "Integra la farina i les gotes de xocolata a la mescla humida sense sobrebatre."),
                Step("Enforna", "Unta el motlle, posa paper a la base i aboca la massa. Enforna fins que quedi fet.", 35),
                Step("Reposa", "Deixa reposar 10-15 minuts dins el motlle abans de desemmotllar.", 15),
                Step("Glasseja", "Prepara el glassejat amb sucre de llustre i suc de taronja. Pinta el plumcake i deixa assecar.")
            ])
    ];

    private static Recipe CreateRecipe(
        string name,
        string description,
        string category,
        int preparationTimeMinutes,
        int cookingTimeMinutes,
        int difficulty,
        int servings,
        RecipeIngredient[] ingredients,
        RecipeStep[] steps)
    {
        var recipe = new Recipe
        {
            Name = name,
            Description = description,
            Category = category,
            PreparationTimeMinutes = preparationTimeMinutes,
            CookingTimeMinutes = cookingTimeMinutes,
            Difficulty = difficulty,
            Servings = servings
        };

        for (var index = 0; index < ingredients.Length; index++)
        {
            ingredients[index].Order = index + 1;
            recipe.Ingredients.Add(ingredients[index]);
        }

        for (var index = 0; index < steps.Length; index++)
        {
            steps[index].Order = index + 1;
            recipe.Steps.Add(steps[index]);
        }

        return recipe;
    }

    private static RecipeIngredient Ingredient(string name, string quantity = "", string unit = "") => new()
    {
        Name = name,
        Quantity = quantity,
        Unit = unit
    };

    private static RecipeStep Step(string title, string instruction, int? timerMinutes = null) => new()
    {
        Title = title,
        Instruction = instruction,
        TimerMinutes = timerMinutes
    };
}
