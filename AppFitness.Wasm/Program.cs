using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AppFitness.Shared.Services;
using AppFitness.Wasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<AppFitness.Shared.Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Device-specific services
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Browser localStorage-based persistence
builder.Services.AddSingleton<ILocalStorageService, BrowserLocalStorageService>();

// Fitness services
builder.Services.AddSingleton<IMealLogService, MealLogService>();
builder.Services.AddSingleton<IWorkoutLogService, WorkoutLogService>();
builder.Services.AddSingleton<IUserProfileService, UserProfileService>();

// HttpClient for Open Food Facts
builder.Services.AddHttpClient<INutritionSearchService, NutritionSearchService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "AppFitness/1.0");
});

await builder.Build().RunAsync();
