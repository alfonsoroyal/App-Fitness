using AppFitness.Web.Components;
using AppFitness.Shared.Services;
using AppFitness.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the AppFitness.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Almacenamiento local en carpeta de datos de la app web
var dataPath = Path.Combine(builder.Environment.ContentRootPath, "AppData");
builder.Services.AddSingleton<ILocalStorageService>(_ => new LocalFileStorageService(dataPath));

// Servicios de fitness
builder.Services.AddSingleton<IMealLogService, MealLogService>();
builder.Services.AddSingleton<IWorkoutLogService, WorkoutLogService>();
builder.Services.AddSingleton<IUserProfileService, UserProfileService>();

// HttpClient para Open Food Facts
builder.Services.AddHttpClient<INutritionSearchService, NutritionSearchService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "AppFitness/1.0");
});

// HttpClient para reconocimiento de alimentos con Clarifai
builder.Services.AddHttpClient<IFoodRecognitionService, FoodRecognitionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AppFitness.Shared._Imports).Assembly);

app.Run();