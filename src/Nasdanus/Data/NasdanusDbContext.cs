using Microsoft.EntityFrameworkCore;
using Nasdanus.Domain;

namespace Nasdanus.Data;

public sealed class NasdanusDbContext(DbContextOptions<NasdanusDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeStepIngredientReference> RecipeStepIngredientReferences => Set<RecipeStepIngredientReference>();
    public DbSet<RecipeNote> RecipeNotes => Set<RecipeNote>();
    public DbSet<RecipePlanningMetadata> RecipePlanningMetadata => Set<RecipePlanningMetadata>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<RecipeCookingSession> RecipeCookingSessions => Set<RecipeCookingSession>();
    public DbSet<MealPlanSlot> MealPlanSlots => Set<MealPlanSlot>();
    public DbSet<MealPlanRecipe> MealPlanRecipes => Set<MealPlanRecipe>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.Property(recipe => recipe.Name).HasMaxLength(160).IsRequired();
            entity.Property(recipe => recipe.Description).HasMaxLength(600);
            entity.Property(recipe => recipe.Category).HasMaxLength(64).IsRequired();
            entity.Property(recipe => recipe.Status).HasMaxLength(32).IsRequired();
            entity.Property(recipe => recipe.SeasonalRecommendation).HasMaxLength(160);

            entity.HasMany(recipe => recipe.Ingredients)
                .WithOne(ingredient => ingredient.Recipe)
                .HasForeignKey(ingredient => ingredient.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.Steps)
                .WithOne(step => step.Recipe)
                .HasForeignKey(step => step.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.Notes)
                .WithOne(note => note.Recipe)
                .HasForeignKey(note => note.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.PlanningMetadata)
                .WithOne(metadata => metadata.Recipe)
                .HasForeignKey(metadata => metadata.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.Tags)
                .WithOne(tag => tag.Recipe)
                .HasForeignKey(tag => tag.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.CookingHistory)
                .WithOne(session => session.Recipe)
                .HasForeignKey(session => session.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.Property(ingredient => ingredient.Name).HasMaxLength(120).IsRequired();
            entity.Property(ingredient => ingredient.Quantity).HasMaxLength(32);
            entity.Property(ingredient => ingredient.Unit).HasMaxLength(48);
            entity.Property(ingredient => ingredient.ScalingMode).HasMaxLength(24).IsRequired();
            entity.HasIndex(ingredient => new { ingredient.RecipeId, ingredient.Order });
        });

        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.Property(step => step.Title).HasMaxLength(120);
            entity.Property(step => step.Instruction).HasMaxLength(1200).IsRequired();
            entity.HasIndex(step => new { step.RecipeId, step.Order }).IsUnique();

            entity.HasMany(step => step.IngredientReferences)
                .WithOne(reference => reference.Step)
                .HasForeignKey(reference => reference.RecipeStepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeStepIngredientReference>(entity =>
        {
            entity.Property(reference => reference.IngredientName).HasMaxLength(120);
            entity.Property(reference => reference.QuantityText).HasMaxLength(32);
            entity.Property(reference => reference.Unit).HasMaxLength(48);

            entity.HasOne(reference => reference.Ingredient)
                .WithMany()
                .HasForeignKey(reference => reference.RecipeIngredientId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(reference => new { reference.RecipeStepId, reference.Order });
        });

        modelBuilder.Entity<RecipeNote>(entity =>
        {
            entity.Property(note => note.Section).HasMaxLength(48).IsRequired();
            entity.Property(note => note.Content).HasMaxLength(1200).IsRequired();
            entity.HasIndex(note => new { note.RecipeId, note.Section, note.Order });
        });

        modelBuilder.Entity<RecipePlanningMetadata>(entity =>
        {
            entity.ToTable("RecipePlanningMetadata");
            entity.Property(metadata => metadata.Kind).HasMaxLength(48).IsRequired();
            entity.Property(metadata => metadata.Value).HasMaxLength(160);
            entity.Property(metadata => metadata.Notes).HasMaxLength(600);
            entity.HasIndex(metadata => new { metadata.RecipeId, metadata.Kind });
        });

        modelBuilder.Entity<RecipeTag>(entity =>
        {
            entity.Property(tag => tag.Name).HasMaxLength(64).IsRequired();
            entity.HasIndex(tag => new { tag.RecipeId, tag.Name }).IsUnique();
        });

        modelBuilder.Entity<RecipeCookingSession>(entity =>
        {
            entity.Property(session => session.Notes).HasMaxLength(1200);
            entity.HasIndex(session => new { session.RecipeId, session.CookedAt });
        });

        modelBuilder.Entity<MealPlanSlot>(entity =>
        {
            entity.Property(slot => slot.MealKind)
                .HasConversion<string>()
                .HasMaxLength(16);

            entity.HasIndex(slot => new { slot.Date, slot.MealKind }).IsUnique();

            entity.HasMany(slot => slot.PlannedRecipes)
                .WithOne(plannedRecipe => plannedRecipe.MealPlanSlot)
                .HasForeignKey(plannedRecipe => plannedRecipe.MealPlanSlotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MealPlanRecipe>(entity =>
        {
            entity.Property(plannedRecipe => plannedRecipe.PlannedServings).HasDefaultValue(0);

            entity.HasOne(plannedRecipe => plannedRecipe.Recipe)
                .WithMany()
                .HasForeignKey(plannedRecipe => plannedRecipe.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(plannedRecipe => new { plannedRecipe.MealPlanSlotId, plannedRecipe.Order });
        });

        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.Property(list => list.WeekStart).IsRequired();
            entity.HasIndex(list => list.WeekStart).IsUnique();

            entity.HasMany(list => list.Items)
                .WithOne(item => item.ShoppingList)
                .HasForeignKey(item => item.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(48).IsRequired();
            entity.Property(item => item.QuantityText).HasMaxLength(48);
            entity.Property(item => item.Unit).HasMaxLength(48);
            entity.HasIndex(item => new { item.ShoppingListId, item.Order });
        });
    }
}
