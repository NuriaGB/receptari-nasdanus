using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using Nasdanus.Components;
using Nasdanus.Data;
using Nasdanus.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "nasdanus.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDbContextFactory<NasdanusDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<PlannerService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<PantryService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
