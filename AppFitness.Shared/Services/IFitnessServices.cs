using AppFitness.Shared.Models;

namespace AppFitness.Shared.Services;

public interface ILocalStorageService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
    Task RemoveAsync(string key);
}

public interface IMealLogService
{
    Task<List<MealEntry>> GetAllAsync();
    Task<List<MealEntry>> GetByDateAsync(DateTime date);
    Task<List<MealEntry>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task SaveAsync(MealEntry entry);
    Task DeleteAsync(Guid id);
    Task<double> GetDailyKcalAsync(DateTime date);
}

public interface IWorkoutLogService
{
    Task<List<WorkoutSession>> GetAllAsync();
    Task<List<WorkoutSession>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task SaveAsync(WorkoutSession session);
    Task DeleteAsync(Guid id);
    Task<List<Exercise>> GetExerciseCatalogAsync();
    Task SaveExerciseAsync(Exercise exercise);
    Task DeleteExerciseAsync(Guid id);
}

public interface IUserProfileService
{
    Task<UserProfile> GetAsync();
    Task SaveAsync(UserProfile profile);
}

public interface INutritionSearchService
{
    Task<List<FoodItem>> SearchFoodAsync(string query);
}

public interface IFoodRecognitionService
{
    /// <summary>Actualiza la API key en runtime.</summary>
    void SetApiKey(string apiKey);

    /// <summary>Indica si hay una API key configurada.</summary>
    bool HasApiKey { get; }

    /// <summary>Establece el modelo a utilizar.</summary>
    void SetModel(string model);

    /// <summary>Lista los modelos disponibles.</summary>
    Task<List<string>> ListAvailableModelsAsync();

    /// <summary>Analiza un texto descriptivo de comida y devuelve ingredientes con macros estimados.</summary>
    Task<FoodAnalysisResult> AnalyzeTextAsync(string description);

    /// <summary>Estima las kcal quemadas a partir de los ejercicios de una sesión.</summary>
    Task<WorkoutAnalysisResult> AnalyzeWorkoutAsync(List<WorkoutSetInput> sets, int durationMinutes, double userWeightKg);
}

/// <summary>Datos de entrada de un ejercicio para el análisis de calorías.</summary>
public class WorkoutSetInput
{
    public string ExerciseName { get; set; } = string.Empty;
    public string MuscleGroup  { get; set; } = string.Empty;
    public int    Sets         { get; set; }
    public int    Reps         { get; set; }
    public double WeightKg     { get; set; }
}

/// <summary>Resultado del análisis de calorías quemadas en un entrenamiento.</summary>
public class WorkoutAnalysisResult
{
    public double TotalKcalBurned { get; set; }
    public List<ExerciseKcalDetail> Details { get; set; } = new();
    public string? Error  { get; set; }
    public bool    Success => Error == null;
}

/// <summary>Detalle de calorías quemadas por ejercicio.</summary>
public class ExerciseKcalDetail
{
    public string ExerciseName  { get; set; } = string.Empty;
    public double KcalBurned    { get; set; }
    public string Notes         { get; set; } = string.Empty;
}

/// <summary>Resultado del análisis de un plato o alimento.</summary>
public class FoodAnalysisResult
{
    /// <summary>Nombre o descripción del plato detectado (ej: "Ensalada César").</summary>
    public string DishName { get; set; } = string.Empty;

    /// <summary>Lista de ingredientes detectados con sus macros estimados.</summary>
    public List<DetectedIngredient> Ingredients { get; set; } = new();

    /// <summary>Mensaje de error si el análisis falló.</summary>
    public string? Error { get; set; }

    public bool Success => Error == null;
}

/// <summary>Un ingrediente detectado con estimación de cantidad y macros.</summary>
public class DetectedIngredient
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Gramos estimados de este ingrediente en el plato.</summary>
    public double EstimatedGrams { get; set; } = 100;

    public double KcalPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }

    // Calculados
    public double TotalKcal    => Math.Round(KcalPer100g    * EstimatedGrams / 100, 1);
    public double TotalProtein => Math.Round(ProteinPer100g * EstimatedGrams / 100, 1);
    public double TotalCarbs   => Math.Round(CarbsPer100g   * EstimatedGrams / 100, 1);
    public double TotalFat     => Math.Round(FatPer100g     * EstimatedGrams / 100, 1);
}
