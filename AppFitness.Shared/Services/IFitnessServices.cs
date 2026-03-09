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
    /// Recibe los bytes de una imagen y devuelve una lista de nombres de alimentos detectados
    /// junto con su confianza (0-1), ordenados de mayor a menor confianza.
    /// </summary>
    Task<List<RecognizedFood>> RecognizeAsync(byte[] imageBytes, string mimeType);
}

public record RecognizedFood(string Name, double Confidence);

