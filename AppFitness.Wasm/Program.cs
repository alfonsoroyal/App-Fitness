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

// Leer el array de API keys desde appsettings.json
// En GitHub Actions se inyectan desde el secret GEMINI_API_KEYS (comas como separador)
// También acepta el antiguo formato de una sola key (GeminiApiKey) por compatibilidad
var geminiKeys = builder.Configuration.GetSection("GeminiApiKeys").Get<string[]>()
                 ?? Array.Empty<string>();

if (geminiKeys.Length == 0)
{
    // Compatibilidad hacia atrás con key única
    var singleKey = builder.Configuration["GeminiApiKey"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(singleKey))
        geminiKeys = new[] { singleKey };
}

var foodHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
builder.Services.AddSingleton<IFoodRecognitionService>(
    new FoodRecognitionService(foodHttpClient, geminiKeys));

await builder.Build().RunAsync();
