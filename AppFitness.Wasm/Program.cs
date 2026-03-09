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

// Leer las API keys de Gemini desde appsettings.json
// GitHub Actions inyecta el secret GEMINI_API_KEY (con comas) como array en GeminiApiKeys
// También acepta GeminiApiKey (singular) por compatibilidad
var geminiKeys = builder.Configuration.GetSection("GeminiApiKeys").Get<string[]>()
                 ?? Array.Empty<string>();

if (geminiKeys.Length == 0)
{
    // Fallback: clave única o múltiples separadas por coma en GeminiApiKey
    var raw = builder.Configuration["GeminiApiKey"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(raw))
    {
        geminiKeys = raw
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToArray();
    }
}

var foodHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
builder.Services.AddSingleton<IFoodRecognitionService>(
    new FoodRecognitionService(foodHttpClient, geminiKeys));

await builder.Build().RunAsync();
