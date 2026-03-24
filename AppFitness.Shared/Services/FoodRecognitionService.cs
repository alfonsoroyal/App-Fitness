using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppFitness.Shared.Models;

namespace AppFitness.Shared.Services;

/// <summary>
/// Análisis nutricional usando Google Gemini (texto).
/// Soporta múltiples API keys con fallback automático al recibir 429 (límite alcanzado).
/// Las keys se inyectan desde el secret GEMINI_API_KEYS de GitHub (separadas por coma).
/// </summary>
public class FoodRecognitionService : IFoodRecognitionService
{
    private readonly HttpClient _http;
    private readonly List<string> _apiKeys;   // keys inyectadas desde GitHub secrets
    private string _manualApiKey = string.Empty; // key configurada manualmente por el usuario
    private string _selectedModel = "gemini-2.5-flash";

    // ─── Propiedades públicas ────────────────────────────────────────────────
    public bool HasApiKey => ActiveKeys.Any();

    /// Todas las keys disponibles: primero la manual, luego las de secrets
    private List<string> ActiveKeys
    {
        get
        {
            var all = new List<string>();
            if (!string.IsNullOrWhiteSpace(_manualApiKey)) all.Add(_manualApiKey);
            all.AddRange(_apiKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
            return all.Distinct().ToList();
        }
    }

    public void SetApiKey(string apiKey) => _manualApiKey = apiKey.Trim();
    public void SetModel(string model)   => _selectedModel = model;

    // Constructor con array de keys (desde appsettings.json inyectado por GitHub Actions)
    public FoodRecognitionService(HttpClient http, string[] apiKeys)
    {
        _http    = http;
        _apiKeys = apiKeys?.Select(k => k.Trim()).Where(k => !string.IsNullOrWhiteSpace(k)).ToList()
                   ?? new List<string>();
    }

    // Constructor legacy con key única
    public FoodRecognitionService(HttpClient http, string apiKey)
        : this(http, string.IsNullOrWhiteSpace(apiKey) ? Array.Empty<string>() : new[] { apiKey }) { }

    // ─── Llamada a Gemini con fallback entre keys ────────────────────────────
    /// <summary>
    /// Intenta la llamada con cada key disponible en orden.
    /// Si una key devuelve 429 o 401/403, pasa automáticamente a la siguiente.
    /// </summary>
    private async Task<(bool ok, string? rawText, string? errorMsg)> CallGeminiAsync(
        object requestBody, CancellationToken ct = default)
    {
        var keys = ActiveKeys;
        if (keys.Count == 0)
            return (false, null, "No hay API keys configuradas. Añade el secret GEMINI_API_KEYS en GitHub → Settings → Secrets and variables → Actions, o configúrala manualmente en Ajustes IA.");

        var json    = JsonSerializer.Serialize(requestBody);
        var baseUrl = $"https://generativelanguage.googleapis.com/v1/models/{_selectedModel}:generateContent";

        string? lastError = null;
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var url = $"{baseUrl}?key={key}";
            try
            {
                using var cts      = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(url, content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var resp    = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cts.Token);
                    var rawText = resp?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
                    return string.IsNullOrEmpty(rawText)
                        ? (false, null, "La IA no devolvió resultado.")
                        : (true, rawText, null);
                }

                var errBody = await response.Content.ReadAsStringAsync();
                var isRateLimit = response.StatusCode == HttpStatusCode.TooManyRequests;
                var isAuthError = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;

                if (isRateLimit)
                {
                    lastError = $"Key {i + 1}/{keys.Count} alcanzó el límite de peticiones (429).";
                    continue; // prueba con la siguiente key
                }
                if (isAuthError)
                {
                    lastError = $"Key {i + 1}/{keys.Count} inválida o sin permisos (401/403).";
                    continue; // prueba con la siguiente key
                }

                // Error no recuperable → no seguir probando
                return (false, null, $"Error del servidor IA ({(int)response.StatusCode}). {TryExtractGeminiError(errBody)}");
            }
            catch (TaskCanceledException)
            {
                lastError = $"Key {i + 1}/{keys.Count}: tiempo de espera agotado.";
                // No hacer fallback en timeout — puede ser problema de red
                return (false, null, "Tiempo de espera agotado. Comprueba tu conexión.");
            }
        }

        // Todas las keys fallaron por límite/auth
        return (false, null,
            $"Todas las API keys han alcanzado el límite o son inválidas. " +
            $"({keys.Count} key{(keys.Count > 1 ? "s" : "")} probada{(keys.Count > 1 ? "s" : "")}). " +
            $"Último error: {lastError}");
    }

    // ─── AnalyzeTextAsync ────────────────────────────────────────────────────
    public async Task<FoodAnalysisResult> AnalyzeTextAsync(string description)
    {
        if (!HasApiKey)
            return Error("No hay API keys configuradas. Añade el secret GEMINI_API_KEYS en GitHub → Settings → Secrets and variables → Actions, o configúrala manualmente en Ajustes IA.");
        if (string.IsNullOrWhiteSpace(description))
            return Error("Introduce una descripción del plato o alimento.");
        try
        {
            var prompt =
                "Eres un nutricionista experto. Dado el siguiente plato o alimento, responde ÚNICAMENTE con un objeto JSON compacto en UNA SOLA LÍNEA, sin saltos de línea, sin espacios extra, sin explicaciones, sin markdown.\n" +
                "Usa EXACTAMENTE este formato (valores numéricos reales, nunca texto):\n" +
                "{\"dish\":\"Nombre del plato\",\"ingredients\":[{\"name\":\"Ingrediente\",\"grams\":100,\"kcal_per_100g\":150,\"protein_per_100g\":10,\"carbs_per_100g\":20,\"fat_per_100g\":5}]}\n" +
                "IMPORTANTE: grams, kcal_per_100g, protein_per_100g, carbs_per_100g y fat_per_100g son siempre números decimales.\n" +
                "Plato a analizar: " + description;

            var body = new
            {
                contents = new[] { new { parts = new object[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.1, maxOutputTokens = 2048 }
            };

            var (ok, rawText, errorMsg) = await CallGeminiAsync(body);
            if (!ok) return Error(errorMsg!);

            rawText = StripMarkdownFences(rawText!);
            rawText = CleanGeminiJson(rawText);

            GeminiDishResult? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiDishResult>(rawText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return Error($"Error al interpretar la respuesta IA.\nJSON:\n{rawText}\n\n{ex.Message}");
            }
            if (parsed == null) return Error("No se pudo interpretar la respuesta de la IA.");

            return new FoodAnalysisResult
            {
                DishName    = parsed.Dish ?? description,
                Ingredients = (parsed.Ingredients ?? new())
                    .Select(i => new DetectedIngredient
                    {
                        Name           = Capitalize(i.Name ?? "Alimento"),
                        EstimatedGrams = Math.Max(1, i.Grams),
                        KcalPer100g    = Math.Max(0, i.KcalPer100g),
                        ProteinPer100g = Math.Max(0, i.ProteinPer100g),
                        CarbsPer100g   = Math.Max(0, i.CarbsPer100g),
                        FatPer100g     = Math.Max(0, i.FatPer100g)
                    }).ToList()
            };
        }
        catch (Exception ex) { return Error($"Error inesperado: {ex.Message}"); }
    }

    // ─── AnalyzeWorkoutAsync ─────────────────────────────────────────────────
    public async Task<WorkoutAnalysisResult> AnalyzeWorkoutAsync(
        List<WorkoutSetInput> sets, int durationMinutes, double userWeightKg)
    {
        if (!HasApiKey)
            return WorkoutError("No hay API keys configuradas. Configura GEMINI_API_KEYS en GitHub Secrets o manualmente en Ajustes IA.");
        if (sets == null || sets.Count == 0)
            return WorkoutError("No hay ejercicios para analizar.");
        try
        {
            var sessionDesc = string.Join(", ", sets.Select(s =>
                $"{s.ExerciseName} ({s.MuscleGroup}): {s.Sets} series x {s.Reps} reps x {s.WeightKg} kg"));

            var prompt =
                "Eres un experto en fisiología del ejercicio. Dado el siguiente entrenamiento:\n" +
                $"Peso del usuario: {userWeightKg} kg. Duración total: {durationMinutes} minutos.\n" +
                "Ejercicios: " + sessionDesc + "\n\n" +
                "Realiza DOS tareas:\n" +
                "1. Estima las calorías quemadas por ejercicio.\n" +
                "2. Analiza la sesión: balance entre ejercicios de empuje (push) y tracción (pull), grupos musculares trabajados, posible exceso de un mismo músculo, falta de variedad, y da recomendaciones concretas para mejorar el plan de entrenamiento o prevenir lesiones.\n\n" +
                "Responde ÚNICAMENTE con un JSON compacto en UNA SOLA LÍNEA, sin markdown.\n" +
                "Formato exacto: {\"total_kcal\":350,\"details\":[{\"exercise\":\"Press banca\",\"kcal\":120,\"notes\":\"Compuesto alta intensidad\"}],\"assessment\":\"Sesión orientada a empuje...\",\"recommendations\":[\"Añade más ejercicios de tracción como dominadas o remo\",\"Considera trabajar el core para equilibrar\"]}\n" +
                "IMPORTANTE: total_kcal y kcal son siempre números. assessment es un string. recommendations es un array de strings con recomendaciones concretas y accionables.";

            var body = new
            {
                contents = new[] { new { parts = new object[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.2, maxOutputTokens = 2048 }
            };

            var (ok, rawText, errorMsg) = await CallGeminiAsync(body);
            if (!ok) return WorkoutError(errorMsg!);

            rawText = StripMarkdownFences(rawText!);
            rawText = CleanGeminiJson(rawText);

            GeminiWorkoutResult? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiWorkoutResult>(rawText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return WorkoutError($"Error al interpretar respuesta IA.\nJSON:\n{rawText}\n{ex.Message}");
            }
            if (parsed == null) return WorkoutError("No se pudo interpretar la respuesta de la IA.");

            return new WorkoutAnalysisResult
            {
                TotalKcalBurned    = Math.Max(0, parsed.TotalKcal),
                Details            = (parsed.Details ?? new())
                    .Select(d => new ExerciseKcalDetail
                    {
                        ExerciseName = Capitalize(d.Exercise ?? "Ejercicio"),
                        KcalBurned   = Math.Max(0, d.Kcal),
                        Notes        = d.Notes ?? string.Empty
                    }).ToList(),
                WorkoutAssessment  = parsed.Assessment ?? string.Empty,
                Recommendations    = parsed.Recommendations ?? new()
            };
        }
        catch (Exception ex) { return WorkoutError($"Error inesperado: {ex.Message}"); }
    }

    // ─── AnalyzeDayDietAsync ─────────────────────────────────────────────────
    public async Task<DietDayAnalysisResult> AnalyzeDayDietAsync(
        List<MealEntry> savedMeals, List<FoodItem> currentMealFoods, string currentMealType, UserProfile profile)
    {
        if (!HasApiKey)
            return DietError("No hay API keys configuradas. Configura GEMINI_API_KEYS en GitHub Secrets o manualmente en Ajustes IA.");
        try
        {
            // Construir resumen de comidas del día
            var mealsDesc = new StringBuilder();

            foreach (var meal in savedMeals)
            {
                var mealName  = meal.MealType.ToString();
                var foodsList = string.Join(", ", meal.Foods.Select(f =>
                    f.Name + " (" + f.QuantityGrams.ToString("0") + "g: " +
                    f.TotalKcal.ToString("0") + "kcal, P:" + f.TotalProtein.ToString("0") + "g, C:" +
                    f.TotalCarbs.ToString("0") + "g, G:" + f.TotalFat.ToString("0") + "g)"));
                mealsDesc.AppendLine("- " + mealName + ": " + foodsList);
            }

            if (currentMealFoods.Any())
            {
                var currentFoods = string.Join(", ", currentMealFoods.Select(f =>
                    f.Name + " (" + f.QuantityGrams.ToString("0") + "g: " +
                    f.TotalKcal.ToString("0") + "kcal, P:" + f.TotalProtein.ToString("0") + "g, C:" +
                    f.TotalCarbs.ToString("0") + "g, G:" + f.TotalFat.ToString("0") + "g)"));
                mealsDesc.AppendLine("- " + currentMealType + " (añadiendo ahora): " + currentFoods);
            }

            var allMealFoods  = savedMeals.SelectMany(m => m.Foods).ToList();
            allMealFoods.AddRange(currentMealFoods);
            double totalKcal    = allMealFoods.Sum(f => (double)f.TotalKcal);
            double totalProtein = allMealFoods.Sum(f => (double)f.TotalProtein);
            double totalCarbs   = allMealFoods.Sum(f => (double)f.TotalCarbs);
            double totalFat     = allMealFoods.Sum(f => (double)f.TotalFat);

            var prompt = new StringBuilder();
            prompt.AppendLine("Eres un nutricionista experto. Analiza la dieta del día completo del usuario y proporciona recomendaciones PERSONALIZADAS y CONCRETAS para alcanzar su objetivo.");
            prompt.AppendLine();
            prompt.AppendLine("PERFIL DEL USUARIO:");
            prompt.AppendLine("- Objetivo: " + profile.Goal);
            prompt.AppendLine("- Objetivo calórico diario: " + profile.DailyCalorieGoal + " kcal");
            prompt.AppendLine("- Peso: " + profile.WeightKg + " kg");
            prompt.AppendLine("- Altura: " + profile.HeightCm + " cm");
            prompt.AppendLine("- Edad: " + profile.Age + " años");
            prompt.AppendLine("- Nivel de actividad: " + profile.ActivityLevel);
            prompt.AppendLine();
            prompt.AppendLine("COMIDAS DEL DÍA:");
            prompt.Append(mealsDesc);
            prompt.AppendLine();
            prompt.AppendLine("TOTALES DEL DÍA:");
            prompt.AppendLine("- Calorías: " + totalKcal.ToString("0") + " kcal (objetivo: " + profile.DailyCalorieGoal + " kcal)");
            prompt.AppendLine("- Proteínas: " + totalProtein.ToString("0") + " g");
            prompt.AppendLine("- Carbohidratos: " + totalCarbs.ToString("0") + " g");
            prompt.AppendLine("- Grasas: " + totalFat.ToString("0") + " g");
            prompt.AppendLine();
            prompt.AppendLine("Analiza si la dieta es adecuada para el objetivo. Indica si hay exceso o déficit de macros, alimentos que debería incluir o reducir, y recomendaciones específicas para el resto del día o días futuros.");
            prompt.AppendLine();
            prompt.AppendLine("Responde ÚNICAMENTE con un JSON compacto en UNA SOLA LÍNEA, sin markdown.");
            prompt.Append("{\"overall\":\"Evaluación general...\",\"recommendations\":[\"Recomendación concreta 1\",\"Recomendación concreta 2\",\"Recomendación concreta 3\"]}");
            prompt.AppendLine();
            prompt.Append("IMPORTANTE: overall es un string. recommendations es un array de 3-5 strings con recomendaciones concretas y accionables.");

            var body = new
            {
                contents = new[] { new { parts = new object[] { new { text = prompt.ToString() } } } },
                generationConfig = new { temperature = 0.3, maxOutputTokens = 2048 }
            };

            var (ok, rawText, errorMsg) = await CallGeminiAsync(body);
            if (!ok) return DietError(errorMsg!);

            rawText = StripMarkdownFences(rawText!);
            rawText = CleanGeminiJson(rawText);

            GeminiDietResult? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiDietResult>(rawText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return DietError("Error al interpretar respuesta IA.\nJSON:\n" + rawText + "\n" + ex.Message);
            }
            if (parsed == null) return DietError("No se pudo interpretar la respuesta de la IA.");

            return new DietDayAnalysisResult
            {
                OverallAssessment = parsed.Overall ?? string.Empty,
                Recommendations   = parsed.Recommendations ?? new List<string>()
            };
        }
        catch (Exception ex) { return DietError("Error inesperado: " + ex.Message); }
    }

    // ─── ListAvailableModelsAsync ─────────────────────────────────────────────
    public async Task<List<string>> ListAvailableModelsAsync()
    {
        var key = ActiveKeys.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            return new List<string> { "Error: no hay API keys configuradas." };

        var url = $"https://generativelanguage.googleapis.com/v1/models?key={key}";
        try
        {
            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var response = await _http.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return new List<string> { $"Error: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var models = new List<string>();
            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
                if (model.TryGetProperty("name", out var name))
                    models.Add(name.GetString() ?? "");
            return models;
        }
        catch (Exception ex) { return new List<string> { $"Exception: {ex.Message}" }; }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private static FoodAnalysisResult    Error(string msg)        => new() { Error = msg };
    private static WorkoutAnalysisResult WorkoutError(string msg) => new() { Error = msg };
    private static DietDayAnalysisResult DietError(string msg)    => new() { Error = msg };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string TryExtractGeminiError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string StripMarkdownFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var first = text.IndexOf('\n');
            var last  = text.LastIndexOf("```", StringComparison.Ordinal);
            text = first > 0 && last > first
                ? text[(first + 1)..last].Trim()
                : first > 0 ? text[(first + 1)..].Trim() : text;
        }
        if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf('\n');
            if (idx > 0) text = text[(idx + 1)..].Trim();
        }
        var fb = text.IndexOf('{');
        if (fb > 0) text = text[fb..];
        var lb = text.LastIndexOf('}');
        if (lb >= 0 && lb < text.Length - 1) text = text[..(lb + 1)];
        return text.Trim();
    }

    private static string CleanGeminiJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([\]}])", "$1");

        // Cuenta comillas no escapadas para detectar cadena sin cerrar
        int quotes = 0; bool esc = false;
        foreach (var c in json)
        {
            if (c == '\\') { esc = !esc; continue; }
            if (c == '"' && !esc) quotes++;
            esc = false;
        }
        if (quotes % 2 != 0) json += "\"";

        // Cierra objetos/arrays abiertos
        var stack = new Stack<char>();
        bool inStr = false; esc = false;
        foreach (var c in json)
        {
            if (esc) { esc = false; continue; }
            if (c == '\\') { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') stack.Push('}');
            else if (c == '[') stack.Push(']');
            else if ((c == '}' || c == ']') && stack.Count > 0 && stack.Peek() == c) stack.Pop();
        }
        while (stack.Count > 0) json += stack.Pop();
        return json.Trim();
    }

    // ─── DTOs Gemini ──────────────────────────────────────────────────────────
    public class GeminiResponse  { [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; } }
    public class GeminiCandidate { [JsonPropertyName("content")]    public GeminiContent? Content { get; set; } }
    public class GeminiContent   { [JsonPropertyName("parts")]      public List<GeminiPart>? Parts { get; set; } }
    public class GeminiPart      { [JsonPropertyName("text")]       public string? Text { get; set; } }

    public class GeminiDishResult
    {
        [JsonPropertyName("dish")]        public string? Dish { get; set; }
        [JsonPropertyName("ingredients")] public List<GeminiIngredient>? Ingredients { get; set; }
    }
    public class GeminiIngredient
    {
        [JsonPropertyName("name")]             public string? Name { get; set; }
        [JsonPropertyName("grams")]            public double Grams { get; set; }
        [JsonPropertyName("kcal_per_100g")]    public double KcalPer100g { get; set; }
        [JsonPropertyName("protein_per_100g")] public double ProteinPer100g { get; set; }
        [JsonPropertyName("carbs_per_100g")]   public double CarbsPer100g { get; set; }
        [JsonPropertyName("fat_per_100g")]     public double FatPer100g { get; set; }
    }

    private class GeminiWorkoutResult
    {
        [JsonPropertyName("total_kcal")]      public double TotalKcal { get; set; }
        [JsonPropertyName("details")]         public List<GeminiExerciseDetail>? Details { get; set; }
        [JsonPropertyName("assessment")]      public string? Assessment { get; set; }
        [JsonPropertyName("recommendations")] public List<string>? Recommendations { get; set; }
    }
    private class GeminiExerciseDetail
    {
        [JsonPropertyName("exercise")] public string? Exercise { get; set; }
        [JsonPropertyName("kcal")]     public double  Kcal     { get; set; }
        [JsonPropertyName("notes")]    public string? Notes    { get; set; }
    }
    private class GeminiDietResult
    {
        [JsonPropertyName("overall")]         public string? Overall { get; set; }
        [JsonPropertyName("recommendations")] public List<string>? Recommendations { get; set; }
    }
}
