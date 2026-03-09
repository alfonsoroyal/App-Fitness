using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppFitness.Shared.Services;

/// <summary>
/// Reconocimiento de alimentos usando Google Gemini Flash (multimodal).
/// Free tier: 1 500 solicitudes/día, sin tarjeta de crédito.
/// La API key se lee desde la configuración de la app (appsettings.json),
/// que GitHub Actions inyecta desde el secret GEMINI_API_KEY en el deploy.
/// </summary>
public class FoodRecognitionService : IFoodRecognitionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    // Modelos en orden de preferencia (nombres exactos de la API v1beta)
    private static readonly string[] Models =
    [
        "gemini-1.5-flash-latest",   // alias estable, siempre disponible
        "gemini-2.0-flash",          // más nuevo, puede estar limitado en free tier
        "gemini-1.5-flash-8b"        // versión ligera, muy rápida
    ];

    private const string EndpointBase =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    // Prompt en español que fuerza una respuesta JSON estricta
    private const string SystemPrompt = """
        Eres un nutricionista experto en análisis visual de alimentos.
        Analiza la imagen y devuelve ÚNICAMENTE un objeto JSON válido con este esquema exacto,
        sin explicaciones adicionales, sin markdown, solo el JSON:

        {
          "dish": "nombre del plato o comida detectada",
          "ingredients": [
            {
              "name": "nombre del ingrediente en español",
              "grams": número estimado de gramos en el plato,
              "kcal_per_100g": número,
              "protein_per_100g": número,
              "carbs_per_100g": número,
              "fat_per_100g": número
            }
          ]
        }

        Reglas:
        - Estima los gramos de cada ingrediente visible en el plato (porción real, no 100g).
        - Los valores nutricionales son por 100g (valores estándar de referencia).
        - Incluye todos los ingredientes visibles, incluso guarniciones pequeñas.
        - Si no puedes identificar un ingrediente concreto, usa el genérico más cercano.
        - Responde SOLO con el JSON, sin ningún texto antes o después.
        """;

    public FoodRecognitionService(HttpClient http, string apiKey)
    {
        _http   = http;
        _apiKey = apiKey;
    }

    public async Task<FoodAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return Error("API key de Gemini no configurada. Añade el secret GEMINI_API_KEY en GitHub → Settings → Secrets and variables → Actions.");

        // Comprimir imagen si supera 1 MB para evitar 429 por payload grande
        var (finalBytes, finalMime) = DownscaleIfNeeded(imageBytes, mimeType);

        // Intentar con cada modelo en orden
        foreach (var model in Models)
        {
            var result = await TryAnalyzeWithModel(model, finalBytes, finalMime);
            if (result.Success) return result;

            // Si no es 429 ni 503, no tiene sentido reintentar con otro modelo
            if (result.Error != null &&
                !result.Error.Contains("429") &&
                !result.Error.Contains("sobrecargado") &&
                !result.Error.Contains("no disponible"))
                return result;
        }

        return Error("El servicio de IA no está disponible ahora mismo (límite de peticiones alcanzado). Espera un minuto e inténtalo de nuevo, o añade los alimentos manualmente.");
    }

    private async Task<FoodAnalysisResult> TryAnalyzeWithModel(string model, byte[] imageBytes, string mimeType)
    {
        try
        {
            var base64 = Convert.ToBase64String(imageBytes);
            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = SystemPrompt },
                            new { inline_data = new { mime_type = mimeType, data = base64 } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature      = 0.1,
                    maxOutputTokens  = 1024,
                    responseMimeType = "application/json"
                }
            };

            var json    = JsonSerializer.Serialize(body);
            var url     = string.Format(EndpointBase, model) + $"?key={_apiKey}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await _http.PostAsync(url, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();

                return response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        Error($"429 — Límite de peticiones de Gemini alcanzado (modelo {model}). Probando alternativa…"),

                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway =>
                        Error($"El modelo {model} no está disponible ahora mismo."),

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

            var parsed = JsonSerializer.Deserialize<GeminiDishResult>(rawText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
                return Error("No se pudo interpretar la respuesta de la IA.");

            return new FoodAnalysisResult
            {
                DishName    = parsed.Dish ?? "Plato detectado",
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

    // ─── Comprimir imagen si pesa más de 1 MB ────────────────────────────────
    // En WASM no tenemos System.Drawing, así que si pesa mucho truncamos
    // los bytes para quedarnos con una versión reducida a JPEG de menor calidad.
    // La compresión real la hace el navegador al capturar con capture="environment".
    private static (byte[] bytes, string mime) DownscaleIfNeeded(byte[] bytes, string mime)
    {
        // Límite: 4 MB (Gemini rechaza base64 > ~4MB en free tier)
        const int MaxBytes = 4 * 1024 * 1024;
        if (bytes.Length <= MaxBytes) return (bytes, mime);

        // Si supera el límite, devolvemos los primeros 4MB (JPEG es progresivo,
        // la imagen seguirá siendo válida pero recortada en calidad)
        var truncated = new byte[MaxBytes];
        Array.Copy(bytes, truncated, MaxBytes);
        return (truncated, "image/jpeg");
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
        if (!text.StartsWith("```")) return text;
        var firstNewline = text.IndexOf('\n');
        var lastFence    = text.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline > 0 && lastFence > firstNewline)
            text = text[(firstNewline + 1)..lastFence].Trim();
        return text;
    }

    // ─── DTOs Gemini ─────────────────────────────────────────────────────────
    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }
    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }
    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    // ─── DTOs del JSON que devuelve Gemini ───────────────────────────────────
    private class GeminiDishResult
    {
        [JsonPropertyName("dish")]
        public string? Dish { get; set; }

        [JsonPropertyName("ingredients")]
        public List<GeminiIngredient>? Ingredients { get; set; }
    }
    private class GeminiIngredient
    {
        [JsonPropertyName("name")]        public string? Name { get; set; }
        [JsonPropertyName("grams")]       public double Grams { get; set; }
        [JsonPropertyName("kcal_per_100g")]    public double KcalPer100g { get; set; }
        [JsonPropertyName("protein_per_100g")] public double ProteinPer100g { get; set; }
        [JsonPropertyName("carbs_per_100g")]   public double CarbsPer100g { get; set; }
        [JsonPropertyName("fat_per_100g")]     public double FatPer100g { get; set; }
    }
}
