﻿using Microsoft.Extensions.Logging;
using AppFitness.Shared.Services;
using AppFitness.Services;

namespace AppFitness;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Add device-specific services used by the AppFitness.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        // Almacenamiento local en FileSystem de MAUI
        builder.Services.AddSingleton<ILocalStorageService>(_ =>
            new LocalFileStorageService(FileSystem.AppDataDirectory));

        // Servicios de fitness
        builder.Services.AddSingleton<IMealLogService, MealLogService>();
        builder.Services.AddSingleton<IWorkoutLogService, WorkoutLogService>();
        builder.Services.AddSingleton<IUserProfileService, UserProfileService>();

        // HttpClient para Open Food Facts
        builder.Services.AddHttpClient<INutritionSearchService, NutritionSearchService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "AppFitness/1.0");
        });

        // HttpClient para reconocimiento de alimentos con Gemini (key desde appsettings.json)
        var geminiApiKey = builder.Configuration["GeminiApiKey"] ?? string.Empty;
        var foodHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        builder.Services.AddSingleton<IFoodRecognitionService>(
            new FoodRecognitionService(foodHttpClient, geminiApiKey));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}