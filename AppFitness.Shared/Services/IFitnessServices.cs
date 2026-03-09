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
    /// <summary>
    /// Analiza una imagen de comida y devuelve los ingredientes detectados
    /// con estimación de gramos, kcal totales y macronutrientes.
    /// </summary>
    Task<FoodAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string mimeType);
}

/// <summary>Resultado completo del análisis de una imagen de comida.</summary>
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

/// <summary>Un ingrediente detectado en la foto con estimación de cantidad y macros.</summary>
public class DetectedIngredient
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Gramos estimados de este ingrediente en el plato.</summary>
    public double EstimatedGrams { get; set; } = 100;

    /// <summary>Kcal por 100 g de este ingrediente.</summary>
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

// Mantener por compatibilidad
public record RecognizedFood(string Name, double Confidence);

