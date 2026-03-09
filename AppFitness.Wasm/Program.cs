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

// HttpClient para reconocimiento de alimentos con Gemini (key desde appsettings.json)
var geminiApiKey = builder.Configuration["GeminiApiKey"] ?? string.Empty;

// Factory manual: evita que DI intente resolver 'string' del constructor
var foodHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
builder.Services.AddSingleton<IFoodRecognitionService>(
    new FoodRecognitionService(foodHttpClient, geminiApiKey));

await builder.Build().RunAsync();
