using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nasdanus.Components;
using Nasdanus.Domain;
using Nasdanus.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMudServices();
builder.Services.AddScoped<BrowserAppStore>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<PlannerService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<PantryService>();
builder.Services.AddScoped<ProductBacklogService>();
builder.Services.AddScoped<NutritionService>();
builder.Services.AddScoped<IngredientKnowledgeService>();
builder.Services.AddScoped<IIngredientNutritionImportService, IngredientNutritionImportService>();

await builder.Build().RunAsync();
