using Microsoft.EntityFrameworkCore;
using Nasdanus.Domain;

namespace Nasdanus.Data;

public sealed class NasdanusDbContext(DbContextOptions<NasdanusDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipePlanningMetadata> RecipePlanningMetadata => Set<RecipePlanningMetadata>();
    public DbSet<MealPlanSlot> MealPlanSlots => Set<MealPlanSlot>();
    public DbSet<MealPlanRecipe> MealPlanRecipes => Set<MealPlanRecipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.Property(recipe => recipe.Name).HasMaxLength(160).IsRequired();
            entity.Property(recipe => recipe.Description).HasMaxLength(600);
            entity.Property(recipe => recipe.Category).HasMaxLength(64).IsRequired();
            entity.Property(recipe => recipe.Status).HasMaxLength(32).IsRequired();

            entity.HasMany(recipe => recipe.Ingredients)
                .WithOne(ingredient => ingredient.Recipe)
                .HasForeignKey(ingredient => ingredient.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.Steps)
                .WithOne(step => step.Recipe)
                .HasForeignKey(step => step.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(recipe => recipe.PlanningMetadata)
                .WithOne(metadata => metadata.Recipe)
                .HasForeignKey(metadata => metadata.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.Property(ingredient => ingredient.Name).HasMaxLength(120).IsRequired();
            entity.Property(ingredient => ingredient.Quantity).HasMaxLength(32);
            entity.Property(ingredient => ingredient.Unit).HasMaxLength(48);
            entity.HasIndex(ingredient => new { ingredient.RecipeId, ingredient.Order });
        });

        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.Property(step => step.Title).HasMaxLength(120);
            entity.Property(step => step.Instruction).HasMaxLength(1200).IsRequired();
            entity.HasIndex(step => new { step.RecipeId, step.Order }).IsUnique();
        });

        modelBuilder.Entity<RecipePlanningMetadata>(entity =>
        {
            entity.ToTable("RecipePlanningMetadata");
            entity.Property(metadata => metadata.Kind).HasMaxLength(48).IsRequired();
            entity.Property(metadata => metadata.Value).HasMaxLength(160);
            entity.Property(metadata => metadata.Notes).HasMaxLength(600);
            entity.HasIndex(metadata => new { metadata.RecipeId, metadata.Kind });
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
            entity.HasOne(plannedRecipe => plannedRecipe.Recipe)
                .WithMany()
                .HasForeignKey(plannedRecipe => plannedRecipe.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(plannedRecipe => new { plannedRecipe.MealPlanSlotId, plannedRecipe.Order });
        });
    }
}
