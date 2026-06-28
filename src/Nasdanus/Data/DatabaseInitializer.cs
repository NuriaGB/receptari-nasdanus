using Microsoft.EntityFrameworkCore;
using Nasdanus.Domain;

namespace Nasdanus.Data;

public sealed class DatabaseInitializer(IDbContextFactory<NasdanusDbContext> dbContextFactory)
{
    public async Task InitializeAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await EnsureRecipeStatusColumnAsync(db);
        await EnsureRecipeFutureColumnsAsync(db);
        await EnsureRecipeIngredientScalingModeColumnAsync(db);
        await EnsureRecipeStepIngredientReferenceTableAsync(db);
        await EnsureRecipeNotesTableAsync(db);
        await EnsureRecipePlanningMetadataTableAsync(db);
        await EnsureRecipeFutureTablesAsync(db);
        await EnsurePlannerRecipeTableAsync(db);
        await EnsureShoppingListTablesAsync(db);

        if (!await db.Recipes.AnyAsync())
        {
            var recipes = SeedRecipes();
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        }

        await ApplyIngredientScalingDefaultsAsync(db);
        await EnsureCurrentWeekSeedAsync(db);
        await EnsureStepIngredientReferenceSeedAsync(db);
    }

    private static async Task EnsureRecipeStatusColumnAsync(NasdanusDbContext db)
    {
        if (await RecipesHasColumnAsync(db, "Status"))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Recipes"
            ADD COLUMN "Status" TEXT NOT NULL DEFAULT 'Active';
        """);
    }

    private static async Task EnsureRecipeFutureColumnsAsync(NasdanusDbContext db)
    {
        if (!await RecipesHasColumnAsync(db, "IsFavourite"))
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Recipes"
                ADD COLUMN "IsFavourite" INTEGER NOT NULL DEFAULT 0;
                """);
        }

        if (!await RecipesHasColumnAsync(db, "Rating"))
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Recipes"
                ADD COLUMN "Rating" INTEGER NULL;
                """);
        }

        if (!await RecipesHasColumnAsync(db, "SeasonalRecommendation"))
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Recipes"
                ADD COLUMN "SeasonalRecommendation" TEXT NOT NULL DEFAULT '';
                """);
        }
    }

    private static async Task EnsureRecipeIngredientScalingModeColumnAsync(NasdanusDbContext db)
    {
        if (await TableHasColumnAsync(db, "RecipeIngredients", "ScalingMode"))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync($"""
            ALTER TABLE "RecipeIngredients"
            ADD COLUMN "ScalingMode" TEXT NOT NULL DEFAULT '{IngredientScalingMode.Linear}';
            """);
    }

    private static async Task<bool> RecipesHasColumnAsync(NasdanusDbContext db, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Recipes')";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task EnsureRecipeNotesTableAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecipeNotes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeNotes" PRIMARY KEY AUTOINCREMENT,
                "RecipeId" INTEGER NOT NULL,
                "Section" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "Order" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_RecipeNotes_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RecipeNotes_RecipeId_Section_Order"
            ON "RecipeNotes" ("RecipeId", "Section", "Order");
            """);
    }

    private static async Task EnsureRecipePlanningMetadataTableAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecipePlanningMetadata" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipePlanningMetadata" PRIMARY KEY AUTOINCREMENT,
                "RecipeId" INTEGER NOT NULL,
                "Kind" TEXT NOT NULL,
                "Value" TEXT NOT NULL DEFAULT '',
                "Notes" TEXT NOT NULL DEFAULT '',
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_RecipePlanningMetadata_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RecipePlanningMetadata_RecipeId_Kind"
            ON "RecipePlanningMetadata" ("RecipeId", "Kind");
            """);
    }

    private static async Task EnsureRecipeFutureTablesAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecipeTags" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeTags" PRIMARY KEY AUTOINCREMENT,
                "RecipeId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                CONSTRAINT "FK_RecipeTags_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeTags_RecipeId_Name"
            ON "RecipeTags" ("RecipeId", "Name");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecipeCookingSessions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeCookingSessions" PRIMARY KEY AUTOINCREMENT,
                "RecipeId" INTEGER NOT NULL,
                "CookedAt" TEXT NOT NULL,
                "PlannedServings" INTEGER NULL,
                "ActualServings" INTEGER NULL,
                "Rating" INTEGER NULL,
                "Notes" TEXT NOT NULL DEFAULT '',
                CONSTRAINT "FK_RecipeCookingSessions_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RecipeCookingSessions_RecipeId_CookedAt"
            ON "RecipeCookingSessions" ("RecipeId", "CookedAt");
            """);
    }

    private static async Task EnsureRecipeStepIngredientReferenceTableAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecipeStepIngredientReferences" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecipeStepIngredientReferences" PRIMARY KEY AUTOINCREMENT,
                "RecipeStepId" INTEGER NOT NULL,
                "RecipeIngredientId" INTEGER NULL,
                "IngredientName" TEXT NOT NULL DEFAULT '',
                "Quantity" TEXT NULL,
                "QuantityText" TEXT NOT NULL DEFAULT '',
                "Unit" TEXT NOT NULL DEFAULT '',
                "Order" INTEGER NOT NULL,
                CONSTRAINT "FK_RecipeStepIngredientReferences_RecipeSteps_RecipeStepId" FOREIGN KEY ("RecipeStepId") REFERENCES "RecipeSteps" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RecipeStepIngredientReferences_RecipeIngredients_RecipeIngredientId" FOREIGN KEY ("RecipeIngredientId") REFERENCES "RecipeIngredients" ("Id") ON DELETE SET NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RecipeStepIngredientReferences_RecipeStepId_Order"
            ON "RecipeStepIngredientReferences" ("RecipeStepId", "Order");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RecipeStepIngredientReferences_RecipeIngredientId"
            ON "RecipeStepIngredientReferences" ("RecipeIngredientId");
            """);
    }

    private static async Task EnsurePlannerRecipeTableAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MealPlanRecipes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MealPlanRecipes" PRIMARY KEY AUTOINCREMENT,
                "MealPlanSlotId" INTEGER NOT NULL,
                "RecipeId" INTEGER NOT NULL,
                "PlannedServings" INTEGER NOT NULL DEFAULT 0,
                "Order" INTEGER NOT NULL,
                CONSTRAINT "FK_MealPlanRecipes_MealPlanSlots_MealPlanSlotId" FOREIGN KEY ("MealPlanSlotId") REFERENCES "MealPlanSlots" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_MealPlanRecipes_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_MealPlanRecipes_MealPlanSlotId_Order"
            ON "MealPlanRecipes" ("MealPlanSlotId", "Order");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_MealPlanRecipes_RecipeId"
            ON "MealPlanRecipes" ("RecipeId");
            """);

        await EnsureMealPlanRecipePlannedServingsColumnAsync(db);

        if (await MealPlanSlotsHasLegacyRecipeIdAsync(db))
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "MealPlanRecipes" ("MealPlanSlotId", "RecipeId", "PlannedServings", "Order")
                SELECT "Id",
                       "RecipeId",
                       COALESCE((SELECT "Servings" FROM "Recipes" WHERE "Recipes"."Id" = "MealPlanSlots"."RecipeId"), 0),
                       1
                FROM "MealPlanSlots"
                WHERE "RecipeId" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM "MealPlanRecipes"
                    WHERE "MealPlanRecipes"."MealPlanSlotId" = "MealPlanSlots"."Id"
                  );
                """);
        }

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "MealPlanRecipes"
            SET "PlannedServings" = COALESCE((
                SELECT "Servings"
                FROM "Recipes"
                WHERE "Recipes"."Id" = "MealPlanRecipes"."RecipeId"
            ), 0)
            WHERE "PlannedServings" <= 0;
            """);
    }

    private static async Task EnsureShoppingListTablesAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ShoppingLists" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ShoppingLists" PRIMARY KEY AUTOINCREMENT,
                "WeekStart" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShoppingLists_WeekStart"
            ON "ShoppingLists" ("WeekStart");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ShoppingListItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ShoppingListItems" PRIMARY KEY AUTOINCREMENT,
                "ShoppingListId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "QuantityText" TEXT NOT NULL DEFAULT '',
                "Unit" TEXT NOT NULL DEFAULT '',
                "Quantity" TEXT NULL,
                "IsChecked" INTEGER NOT NULL DEFAULT 0,
                "IsManual" INTEGER NOT NULL DEFAULT 0,
                "Order" INTEGER NOT NULL,
                CONSTRAINT "FK_ShoppingListItems_ShoppingLists_ShoppingListId" FOREIGN KEY ("ShoppingListId") REFERENCES "ShoppingLists" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ShoppingListItems_ShoppingListId_Order"
            ON "ShoppingListItems" ("ShoppingListId", "Order");
            """);
    }

    private static async Task EnsureMealPlanRecipePlannedServingsColumnAsync(NasdanusDbContext db)
    {
        if (await TableHasColumnAsync(db, "MealPlanRecipes", "PlannedServings"))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "MealPlanRecipes"
            ADD COLUMN "PlannedServings" INTEGER NOT NULL DEFAULT 0;
            """);
    }

    private static async Task<bool> MealPlanSlotsHasLegacyRecipeIdAsync(NasdanusDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('MealPlanSlots')";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), "RecipeId", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TableHasColumnAsync(NasdanusDbContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}')";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ApplyIngredientScalingDefaultsAsync(NasdanusDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync($"""
            UPDATE "RecipeIngredients"
            SET "ScalingMode" = '{IngredientScalingMode.ToTaste}'
            WHERE "ScalingMode" = '{IngredientScalingMode.Linear}'
              AND (
                lower("Name") = 'sal'
                OR lower("Name") LIKE '%pebre%'
                OR lower("Name") LIKE '%jalape%'
                OR lower("Name") LIKE '%bitxo%'
                OR lower("Name") LIKE '%xili%'
              );
            """);

        await db.Database.ExecuteSqlRawAsync($"""
            UPDATE "RecipeIngredients"
            SET "ScalingMode" = '{IngredientScalingMode.Approximate}'
            WHERE "ScalingMode" = '{IngredientScalingMode.Linear}'
              AND (
                lower("Name") LIKE '%oli%'
                OR lower("Name") LIKE '%all%'
                OR lower("Name") LIKE '%gingebre%'
                OR lower("Name") LIKE '%herba%'
                OR lower("Name") LIKE '%julivert%'
                OR lower("Name") LIKE '%llimona%'
                OR lower("Name") LIKE '%taronja%'
                OR lower("Name") LIKE '%cúrcuma%'
                OR lower("Name") LIKE '%curcuma%'
                OR lower("Name") LIKE '%garam masala%'
              );
            """);
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

        var lunch = await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes)
            .FirstAsync(slot => slot.Date == today && slot.MealKind == MealKind.Lunch);
        var dinner = await db.MealPlanSlots
            .Include(slot => slot.PlannedRecipes)
            .FirstAsync(slot => slot.Date == today && slot.MealKind == MealKind.Dinner);

        if (lunch.PlannedRecipes.Count == 0)
        {
            var lunchRecipe = await db.Recipes
                .Where(recipe => recipe.Name == "Samuses de vedella amb verduretes a l'airfryer")
                .Select(recipe => new { recipe.Id, recipe.Servings })
                .FirstAsync();

            lunch.PlannedRecipes.Add(new MealPlanRecipe
            {
                RecipeId = lunchRecipe.Id,
                PlannedServings = lunchRecipe.Servings,
                Order = 1
            });
        }

        if (dinner.PlannedRecipes.Count == 0)
        {
            var dinnerRecipe = await db.Recipes
                .Where(recipe => recipe.Name == "Cassoleta de verdures, pollastre i patata bullida")
                .Select(recipe => new { recipe.Id, recipe.Servings })
                .FirstAsync();

            dinner.PlannedRecipes.Add(new MealPlanRecipe
            {
                RecipeId = dinnerRecipe.Id,
                PlannedServings = dinnerRecipe.Servings,
                Order = 1
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureStepIngredientReferenceSeedAsync(NasdanusDbContext db)
    {
        var recipes = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Steps)
                .ThenInclude(step => step.IngredientReferences)
            .ToListAsync();

        var changed = false;
        foreach (var recipe in recipes)
        {
            foreach (var step in recipe.Steps)
            {
                if (step.IngredientReferences.Count > 0)
                {
                    continue;
                }

                var matchedIngredients = recipe.Ingredients
                    .Where(ingredient => IngredientAppearsInStep(step, ingredient))
                    .OrderBy(ingredient => ingredient.Order)
                    .ToList();

                for (var index = 0; index < matchedIngredients.Count; index++)
                {
                    var ingredient = matchedIngredients[index];
                    step.IngredientReferences.Add(new RecipeStepIngredientReference
                    {
                        RecipeIngredientId = ingredient.Id,
                        IngredientName = ingredient.Name,
                        Quantity = IngredientScaling.ParseQuantity(ingredient.Quantity),
                        QuantityText = ingredient.Quantity,
                        Unit = ingredient.Unit,
                        Order = index + 1
                    });
                }

                changed = changed || matchedIngredients.Count > 0;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static bool IngredientAppearsInStep(RecipeStep step, RecipeIngredient ingredient)
    {
        if (string.IsNullOrWhiteSpace(ingredient.Name))
        {
            return false;
        }

        var stepText = $"{step.Title} {step.Instruction}";
        return stepText.Contains(ingredient.Name, StringComparison.OrdinalIgnoreCase);
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
        Unit = unit,
        ScalingMode = InferScalingMode(name)
    };

    private static string InferScalingMode(string name)
    {
        var normalized = name.ToLowerInvariant();
        if (normalized is "sal" || normalized.Contains("pebre") || normalized.Contains("jalape") || normalized.Contains("bitxo") || normalized.Contains("xili"))
        {
            return IngredientScalingMode.ToTaste;
        }

        if (normalized.Contains("oli")
            || normalized.Contains("all")
            || normalized.Contains("gingebre")
            || normalized.Contains("herba")
            || normalized.Contains("julivert")
            || normalized.Contains("llimona")
            || normalized.Contains("taronja")
            || normalized.Contains("cúrcuma")
            || normalized.Contains("curcuma")
            || normalized.Contains("garam masala"))
        {
            return IngredientScalingMode.Approximate;
        }

        return IngredientScalingMode.Linear;
    }

    private static RecipeStep Step(string title, string instruction, int? timerMinutes = null) => new()
    {
        Title = title,
        Instruction = instruction,
        TimerMinutes = timerMinutes
    };
}
