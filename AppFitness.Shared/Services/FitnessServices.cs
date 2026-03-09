using System.Text.Json;
using AppFitness.Shared.Models;

namespace AppFitness.Shared.Services;

/// <summary>
/// Implementación base de almacenamiento local usando archivos JSON.
/// Usada en MAUI (FileSystem.AppDataDirectory).
/// </summary>
public class LocalFileStorageService : ILocalStorageService
{
    private readonly string _basePath;
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalFileStorageService(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    private string GetFilePath(string key) => Path.Combine(_basePath, $"{key}.json");

    public async Task<T?> GetAsync<T>(string key)
    {
        var path = GetFilePath(key);
        if (!File.Exists(path)) return default;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        var path = GetFilePath(key);
        var json = JsonSerializer.Serialize(value, _options);
        await File.WriteAllTextAsync(path, json);
    }

    public Task RemoveAsync(string key)
    {
        var path = GetFilePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

public class MealLogService : IMealLogService
{
    private readonly ILocalStorageService _storage;
    private const string Key = "meal_entries";

    public MealLogService(ILocalStorageService storage) => _storage = storage;

    public async Task<List<MealEntry>> GetAllAsync()
        => await _storage.GetAsync<List<MealEntry>>(Key) ?? new List<MealEntry>();

    public async Task<List<MealEntry>> GetByDateAsync(DateTime date)
    {
        var all = await GetAllAsync();
        return all.Where(m => m.Date.Date == date.Date).ToList();
    }

    public async Task<List<MealEntry>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        var all = await GetAllAsync();
        return all.Where(m => m.Date.Date >= from.Date && m.Date.Date <= to.Date).ToList();
    }

    public async Task SaveAsync(MealEntry entry)
    {
        var all = await GetAllAsync();
        var idx = all.FindIndex(m => m.Id == entry.Id);
        if (idx >= 0) all[idx] = entry;
        else all.Add(entry);
        await _storage.SetAsync(Key, all);
    }

    public async Task DeleteAsync(Guid id)
    {
        var all = await GetAllAsync();
        all.RemoveAll(m => m.Id == id);
        await _storage.SetAsync(Key, all);
    }

    public async Task<double> GetDailyKcalAsync(DateTime date)
    {
        var meals = await GetByDateAsync(date);
        return meals.Sum(m => m.TotalKcal);
    }
}

public class WorkoutLogService : IWorkoutLogService
{
    private readonly ILocalStorageService _storage;
    private const string SessionsKey = "workout_sessions";
    private const string ExercisesKey = "exercise_catalog";

    public WorkoutLogService(ILocalStorageService storage) => _storage = storage;

    public async Task<List<WorkoutSession>> GetAllAsync()
        => await _storage.GetAsync<List<WorkoutSession>>(SessionsKey) ?? new List<WorkoutSession>();

    public async Task<List<WorkoutSession>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        var all = await GetAllAsync();
        return all.Where(s => s.Date.Date >= from.Date && s.Date.Date <= to.Date)
                  .OrderByDescending(s => s.Date).ToList();
    }

    public async Task SaveAsync(WorkoutSession session)
    {
        var all = await GetAllAsync();
        var idx = all.FindIndex(s => s.Id == session.Id);
        if (idx >= 0) all[idx] = session;
        else all.Add(session);
        await _storage.SetAsync(SessionsKey, all);
    }

    public async Task DeleteAsync(Guid id)
    {
        var all = await GetAllAsync();
        all.RemoveAll(s => s.Id == id);
        await _storage.SetAsync(SessionsKey, all);
    }

    public async Task<List<Exercise>> GetExerciseCatalogAsync()
    {
        var catalog = await _storage.GetAsync<List<Exercise>>(ExercisesKey);
        if (catalog != null && catalog.Count > 0) return catalog;
        // Catálogo por defecto
        return GetDefaultExercises();
    }

    public async Task SaveExerciseAsync(Exercise exercise)
    {
        var catalog = await GetExerciseCatalogAsync();
        var idx = catalog.FindIndex(e => e.Id == exercise.Id);
        if (idx >= 0) catalog[idx] = exercise;
        else catalog.Add(exercise);
        await _storage.SetAsync(ExercisesKey, catalog);
    }

    public async Task DeleteExerciseAsync(Guid id)
    {
        var catalog = await GetExerciseCatalogAsync();
        catalog.RemoveAll(e => e.Id == id);
        await _storage.SetAsync(ExercisesKey, catalog);
    }

    private static List<Exercise> GetDefaultExercises() => new()
    {
        new() { Name = "Press Banca", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Sentadilla", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Peso Muerto", MuscleGroup = "Espalda/Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press Militar", MuscleGroup = "Hombros", Category = ExerciseCategory.Fuerza },
        new() { Name = "Dominadas", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl Bíceps", MuscleGroup = "Bíceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press Francés", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Remo con Barra", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Jalón al Pecho", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Fondos", MuscleGroup = "Tríceps/Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Zancadas", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Hip Thrust", MuscleGroup = "Glúteos", Category = ExerciseCategory.Fuerza },
        new() { Name = "Correr", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Bicicleta", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Elíptica", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Remo (máquina)", MuscleGroup = "Cardio/Espalda", Category = ExerciseCategory.Cardio },
        new() { Name = "Plancha", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Abdominales", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
    };
}

public class UserProfileService : IUserProfileService
{
    private readonly ILocalStorageService _storage;
    private const string Key = "user_profile";

    public UserProfileService(ILocalStorageService storage) => _storage = storage;

    public async Task<UserProfile> GetAsync()
        => await _storage.GetAsync<UserProfile>(Key) ?? new UserProfile();

    public async Task SaveAsync(UserProfile profile)
        => await _storage.SetAsync(Key, profile);
}

