using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppFitness.Shared.Services;

/// <summary>
/// Análisis nutricional de alimentos usando Google Gemini (texto).
/// Free tier: 1500 solicitudes/día, sin tarjeta de crédito.
/// La API key se carga desde localStorage (configurada por el usuario en Settings).
/// </summary>
public class FoodRecognitionService : IFoodRecognitionService
{
    private readonly HttpClient _http;
    private string _apiKey;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

    public void SetApiKey(string apiKey) => _apiKey = apiKey.Trim();

    private string _selectedModel = "gemini-2.5-flash";

    public void SetModel(string model) => _selectedModel = model;

    public FoodRecognitionService(HttpClient http, string apiKey)
    {
        _http   = http;
        _apiKey = apiKey?.Trim() ?? string.Empty;
    }

    public async Task<FoodAnalysisResult> AnalyzeTextAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return Error("API key de Gemini no configurada. Añade el secret GEMINI_API_KEY en GitHub → Settings → Secrets and variables → Actions.");
        if (string.IsNullOrWhiteSpace(description))
            return Error("Introduce una descripción del plato o alimento.");
        try
        {
            var prompt =
                "Eres un nutricionista experto. Dado el siguiente plato o alimento, responde ÚNICAMENTE con un objeto JSON compacto en UNA SOLA LÍNEA, sin saltos de línea, sin espacios extra, sin explicaciones, sin markdown.\n" +
                "Usa EXACTAMENTE este formato (valores numéricos reales, nunca texto):\n" +
                "{\"dish\":\"Nombre del plato\",\"ingredients\":[{\"name\":\"Ingrediente\",\"grams\":100,\"kcal_per_100g\":150,\"protein_per_100g\":10,\"carbs_per_100g\":20,\"fat_per_100g\":5},{\"name\":\"Ingrediente2\",\"grams\":50,\"kcal_per_100g\":200,\"protein_per_100g\":5,\"carbs_per_100g\":30,\"fat_per_100g\":8}]}\n" +
                "IMPORTANTE: grams, kcal_per_100g, protein_per_100g, carbs_per_100g y fat_per_100g son siempre números decimales.\n" +
                "Plato a analizar: " + description;
            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature     = 0.1,
                    maxOutputTokens = 2048
                }
            };
            var json    = JsonSerializer.Serialize(body);
            var endpoint = $"https://generativelanguage.googleapis.com/v1/models/{_selectedModel}:generateContent";
            var url     = endpoint + $"?key={_apiKey}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await _http.PostAsync(url, content, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        Error($"429 — Límite de peticiones de Gemini alcanzado. Espera un minuto e inténtalo de nuevo."),
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway =>
                        Error($"El modelo {_selectedModel} no está disponible ahora mismo."),
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                        Error("API key de Gemini inválida o sin permisos. Comprueba el secret GEMINI_API_KEY en GitHub → Settings → Secrets."),
                    _ => Error($"Error del servidor IA ({(int)response.StatusCode}). {TryExtractGeminiError(errBody)}")
                };
            }
            var geminiResp = await response.Content.ReadFromJsonAsync<GeminiResponse>();
            var rawText    = geminiResp?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrEmpty(rawText))
                return Error("La IA no devolvió resultado.");
            rawText = StripMarkdownFences(rawText);
            rawText = CleanGeminiJson(rawText);
            GeminiDishResult? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiDishResult>(rawText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return Error($"Error al interpretar la respuesta de la IA.\nJSON recibido:\n{rawText}\n\n{ex.Message}");
            }
            if (parsed == null)
                return Error("No se pudo interpretar la respuesta de la IA.");
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
                    })
                    .ToList()
            };
        }
        catch (TaskCanceledException)
        {
            return Error("Tiempo de espera agotado. Comprueba tu conexión e inténtalo de nuevo.");
        }
        catch (Exception ex)
        {
            return Error($"Error inesperado: {ex.Message}");
        }
    }
    // ─── Extraer mensaje legible del error JSON de Gemini ────────────────────
    private static string TryExtractGeminiError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();
            return msg ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private static FoodAnalysisResult Error(string msg) => new() { Error = msg };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string StripMarkdownFences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = text.Trim();

        // Elimina bloque ```json ... ``` o ``` ... ```
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence    = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
            else if (firstNewline > 0)
                text = text[(firstNewline + 1)..].Trim();
        }

        // Elimina línea inicial "json" o "JSON"
        if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf('\n');
            if (idx > 0) text = text[(idx + 1)..].Trim();
        }

        // Extrae desde el primer '{' hasta el último '}'
        var firstBrace = text.IndexOf('{');
        if (firstBrace > 0) text = text[firstBrace..];
        var lastBrace = text.LastIndexOf('}');
        if (lastBrace >= 0 && lastBrace < text.Length - 1)
            text = text[..(lastBrace + 1)];

        return text.Trim();
    }

    // Repara JSON incompleto: cierra cadenas, objetos y arrays abiertos
    private static string CleanGeminiJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        // Elimina comas finales antes de ] o }
        json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([\]}])", "$1");

        // Cierra cadenas sin cerrar (número impar de comillas dobles no escapadas)
        int quotes = 0;
        bool escaped = false;
        foreach (char c in json)
        {
            if (c == '\\') { escaped = !escaped; continue; }
            if (c == '"' && !escaped) quotes++;
            escaped = false;
        }
        if (quotes % 2 != 0) json += "\"";

        // Cierra objetos y arrays abiertos desde el final
        var stack = new System.Collections.Generic.Stack<char>();
        bool inStr = false;
        escaped = false;
        foreach (char c in json)
        {
            if (escaped) { escaped = false; continue; }
            if (c == '\\') { escaped = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') stack.Push('}');
            else if (c == '[') stack.Push(']');
            else if (c == '}' || c == ']')
            {
                if (stack.Count > 0 && stack.Peek() == c) stack.Pop();
            }
        }
        // Añade los cierres que faltan en orden inverso
        while (stack.Count > 0)
            json += stack.Pop();

        return json.Trim();
    }

    // ─── Análisis de calorías quemadas en entrenamiento ─────────────────────
    public async Task<WorkoutAnalysisResult> AnalyzeWorkoutAsync(
        List<WorkoutSetInput> sets, int durationMinutes, double userWeightKg)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return WorkoutError("API key de Gemini no configurada.");
        if (sets == null || sets.Count == 0)
            return WorkoutError("No hay ejercicios para analizar.");
        try
        {
            // Construir descripción de la sesión
            var setLines = sets.Select(s =>
                $"{s.ExerciseName} ({s.MuscleGroup}): {s.Sets} series x {s.Reps} reps x {s.WeightKg} kg");
            var sessionDesc = string.Join(", ", setLines);

            var prompt =
                "Eres un experto en fisiología del ejercicio. Dado el siguiente entrenamiento, estima las calorías quemadas.\n" +
                $"Peso del usuario: {userWeightKg} kg. Duración total: {durationMinutes} minutos.\n" +
                "Ejercicios realizados: " + sessionDesc + "\n" +
                "Responde ÚNICAMENTE con un objeto JSON compacto en UNA SOLA LÍNEA, sin saltos de línea, sin markdown.\n" +
                "Usa EXACTAMENTE este formato (valores numéricos reales):\n" +
                "{\"total_kcal\":350,\"details\":[{\"exercise\":\"Press banca\",\"kcal\":120,\"notes\":\"Ejercicio compuesto alta intensidad\"},{\"exercise\":\"Sentadilla\",\"kcal\":230,\"notes\":\"Tren inferior, alta demanda energética\"}]}\n" +
                "IMPORTANTE: total_kcal y kcal son siempre números, nunca texto.";

            var body = new
            {
                contents = new[]
                {
                    new { parts = new object[] { new { text = prompt } } }
                },
                generationConfig = new { temperature = 0.1, maxOutputTokens = 1024 }
            };

            var json     = JsonSerializer.Serialize(body);
            var url      = $"https://generativelanguage.googleapis.com/v1/models/{_selectedModel}:generateContent?key={_apiKey}";
            var content  = new StringContent(json, Encoding.UTF8, "application/json");
            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await _http.PostAsync(url, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests  => WorkoutError("429 — Límite de peticiones alcanzado. Espera un minuto."),
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => WorkoutError("API key inválida o sin permisos."),
                    _ => WorkoutError($"Error del servidor IA ({(int)response.StatusCode}). {TryExtractGeminiError(errBody)}")
                };
            }

            var geminiResp = await response.Content.ReadFromJsonAsync<GeminiResponse>();
            var rawText    = geminiResp?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrEmpty(rawText))
                return WorkoutError("La IA no devolvió resultado.");

            rawText = StripMarkdownFences(rawText);
            rawText = CleanGeminiJson(rawText);

            GeminiWorkoutResult? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<GeminiWorkoutResult>(rawText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                return WorkoutError($"Error al interpretar respuesta IA.\nJSON:\n{rawText}\n{ex.Message}");
            }

            if (parsed == null)
                return WorkoutError("No se pudo interpretar la respuesta de la IA.");

            return new WorkoutAnalysisResult
            {
                TotalKcalBurned = Math.Max(0, parsed.TotalKcal),
                Details = (parsed.Details ?? new())
                    .Select(d => new ExerciseKcalDetail
                    {
                        ExerciseName = Capitalize(d.Exercise ?? "Ejercicio"),
                        KcalBurned   = Math.Max(0, d.Kcal),
                        Notes        = d.Notes ?? string.Empty
                    })
                    .ToList()
            };
        }
        catch (TaskCanceledException)
        {
            return WorkoutError("Tiempo de espera agotado.");
        }
        catch (Exception ex)
        {
            return WorkoutError($"Error inesperado: {ex.Message}");
        }
    }

    private static WorkoutAnalysisResult WorkoutError(string msg) => new() { Error = msg };

    // ─── DTO workout Gemini ───────────────────────────────────────────────────
    private class GeminiWorkoutResult
    {
        [JsonPropertyName("total_kcal")] public double TotalKcal { get; set; }
        [JsonPropertyName("details")]    public List<GeminiExerciseDetail>? Details { get; set; }
    }
    private class GeminiExerciseDetail
    {
        [JsonPropertyName("exercise")] public string? Exercise { get; set; }
        [JsonPropertyName("kcal")]     public double  Kcal     { get; set; }
        [JsonPropertyName("notes")]    public string? Notes    { get; set; }
    }
    public async Task<List<string>> ListAvailableModelsAsync()
    {
        var url = $"https://generativelanguage.googleapis.com/v1/models?key={_apiKey}";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var response = await _http.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return new List<string> { $"Error: {response.StatusCode} {await response.Content.ReadAsStringAsync()}" };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var models = new List<string>();
            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name))
                    models.Add(name.GetString() ?? "");
            }
            return models;
        }
        catch (Exception ex)
        {
            return new List<string> { $"Exception: {ex.Message}" };
        }
    }

    // ─── DTOs Gemini ─────────────────────────────────────────────────────────
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }
    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }
    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    // ─── DTOs del JSON que devuelve Gemini ───────────────────────────────────
    public class GeminiDishResult
    {
        [JsonPropertyName("dish")]
        public string? Dish { get; set; }

        [JsonPropertyName("ingredients")]
        public List<GeminiIngredient>? Ingredients { get; set; }
    }
    public class GeminiIngredient
    {
        [JsonPropertyName("name")]        public string? Name { get; set; }
        [JsonPropertyName("grams")]       public double Grams { get; set; }
        [JsonPropertyName("kcal_per_100g")]    public double KcalPer100g { get; set; }
        [JsonPropertyName("protein_per_100g")] public double ProteinPer100g { get; set; }
        [JsonPropertyName("carbs_per_100g")]   public double CarbsPer100g { get; set; }
        [JsonPropertyName("fat_per_100g")]     public double FatPer100g { get; set; }
    }
}
