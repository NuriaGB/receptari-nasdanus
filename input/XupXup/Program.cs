using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RecetariBlazor;
using RecetariBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HTTP client per a crides a Google Drive API
builder.Services.AddScoped(sp => new HttpClient());

// HTTP client amb BaseAddress per a fitxers estàtics (ingredients-db.json, etc.)
builder.Services.AddScoped<IngredientDbService>(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    return new IngredientDbService(http);
});

// MudBlazor
builder.Services.AddMudServices();

// Serveis propis
builder.Services.AddScoped<GoogleDriveService>();
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();
