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
        // Ejercicios originales
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
        // Ejercicios adicionales
        new() { Name = "Press de hombro con mancuernas", MuscleGroup = "Hombros", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl martillo", MuscleGroup = "Bíceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Extensión de tríceps en polea", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Aperturas con mancuernas", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Sentadilla búlgara", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press de piernas", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Crunch abdominal", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Mountain climbers", MuscleGroup = "Core/Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Burpees", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Jumping jacks", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Estiramiento de cuádriceps", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de pectoral", MuscleGroup = "Pecho", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Yoga", MuscleGroup = "Flexibilidad", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Pilates", MuscleGroup = "Flexibilidad/Core", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Escalera", MuscleGroup = "Cardio/Piernas", Category = ExerciseCategory.Cardio },
        new() { Name = "Remo con mancuernas", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl de piernas", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press de pecho en máquina", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Tríceps en banco", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Bicicleta estática", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Caminata", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        // 50 ejercicios nuevos
        new() { Name = "Press inclinado", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press declinado", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Remo sentado", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Remo en máquina", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Remo unilateral", MuscleGroup = "Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl concentrado", MuscleGroup = "Bíceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl en banco Scott", MuscleGroup = "Bíceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl en polea", MuscleGroup = "Bíceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Extensión de piernas", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Curl femoral", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Elevación de talones", MuscleGroup = "Gemelos", Category = ExerciseCategory.Fuerza },
        new() { Name = "Elevación lateral", MuscleGroup = "Hombros", Category = ExerciseCategory.Fuerza },
        new() { Name = "Elevación frontal", MuscleGroup = "Hombros", Category = ExerciseCategory.Fuerza },
        new() { Name = "Face pull", MuscleGroup = "Hombros/Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Pull-over", MuscleGroup = "Espalda/Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press Arnold", MuscleGroup = "Hombros", Category = ExerciseCategory.Fuerza },
        new() { Name = "Press de triceps en máquina", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Tríceps en cuerda", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Tríceps en barra", MuscleGroup = "Tríceps", Category = ExerciseCategory.Fuerza },
        new() { Name = "Crunch oblicuo", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Russian twist", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Elevación de piernas", MuscleGroup = "Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Superman", MuscleGroup = "Core/Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Jump squat", MuscleGroup = "Piernas/Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Sprint", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Saltar la cuerda", MuscleGroup = "Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "TRX press", MuscleGroup = "Pecho", Category = ExerciseCategory.Fuerza },
        new() { Name = "Kettlebell swing", MuscleGroup = "Piernas/Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Kettlebell snatch", MuscleGroup = "Piernas/Core", Category = ExerciseCategory.Fuerza },
        new() { Name = "Farmer's walk", MuscleGroup = "Core/Brazos", Category = ExerciseCategory.Fuerza },
        new() { Name = "Peso muerto sumo", MuscleGroup = "Piernas/Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Peso muerto rumano", MuscleGroup = "Piernas/Espalda", Category = ExerciseCategory.Fuerza },
        new() { Name = "Sentadilla sumo", MuscleGroup = "Piernas", Category = ExerciseCategory.Fuerza },
        new() { Name = "Sentadilla con salto", MuscleGroup = "Piernas/Cardio", Category = ExerciseCategory.Cardio },
        new() { Name = "Estiramiento lumbar", MuscleGroup = "Espalda", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de isquiotibiales", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de gemelos", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Movilidad articular", MuscleGroup = "Flexibilidad", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Rotación de tronco", MuscleGroup = "Core/Flexibilidad", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de dorsal", MuscleGroup = "Espalda", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de triceps", MuscleGroup = "Tríceps", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de bíceps", MuscleGroup = "Bíceps", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de glúteos", MuscleGroup = "Glúteos", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de aductores", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de abductores", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento cervical", MuscleGroup = "Cuello", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de antebrazo", MuscleGroup = "Brazos", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Estiramiento de muñeca", MuscleGroup = "Brazos", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Movilidad de tobillo", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Movilidad de hombro", MuscleGroup = "Hombros", Category = ExerciseCategory.Flexibilidad },
        new() { Name = "Movilidad de cadera", MuscleGroup = "Piernas", Category = ExerciseCategory.Flexibilidad },
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
