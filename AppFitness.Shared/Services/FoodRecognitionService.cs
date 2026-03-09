using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppFitness.Shared.Models;

namespace AppFitness.Shared.Services;

/// <summary>
/// Reconocimiento de alimentos en imágenes usando la API pública de Clarifai
/// (modelo food-item-recognition, free tier: 1000 operaciones/mes).
/// Documentación: https://clarifai.com/clarifai/main/models/food-item-recognition
/// </summary>
public class FoodRecognitionService : IFoodRecognitionService
{
    private readonly HttpClient _http;

    // API Key de Clarifai (Personal Access Token público de demo)
    // El usuario puede sustituirla por la suya en https://clarifai.com (registro gratuito)
    private const string ClarifaiPat = "a6809d08d3e142ca9c2f3fa1dc012daa";
    private const string ModelUrl =
        "https://api.clarifai.com/v2/models/food-item-recognition/versions/1d5fd481e0cf4826aa72ec3ff049e4d5/outputs";

    // Mapeo de términos en inglés a español para mejorar la búsqueda nutricional
    private static readonly Dictionary<string, string> EnToEs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["apple"] = "manzana", ["banana"] = "plátano", ["orange"] = "naranja",
        ["strawberry"] = "fresa", ["grape"] = "uva", ["watermelon"] = "sandía",
        ["pineapple"] = "piña", ["mango"] = "mango", ["peach"] = "melocotón",
        ["lemon"] = "limón", ["cherry"] = "cereza", ["pear"] = "pera",
        ["chicken"] = "pollo", ["beef"] = "ternera", ["pork"] = "cerdo",
        ["fish"] = "pescado", ["salmon"] = "salmón", ["tuna"] = "atún",
        ["egg"] = "huevo", ["cheese"] = "queso", ["milk"] = "leche",
        ["yogurt"] = "yogur", ["butter"] = "mantequilla",
        ["rice"] = "arroz", ["pasta"] = "pasta", ["bread"] = "pan",
        ["potato"] = "patata", ["tomato"] = "tomate", ["lettuce"] = "lechuga",
        ["carrot"] = "zanahoria", ["broccoli"] = "brócoli", ["spinach"] = "espinacas",
        ["onion"] = "cebolla", ["garlic"] = "ajo", ["pepper"] = "pimiento",
        ["mushroom"] = "champiñón", ["cucumber"] = "pepino", ["corn"] = "maíz",
        ["pizza"] = "pizza", ["hamburger"] = "hamburguesa", ["sandwich"] = "sándwich",
        ["salad"] = "ensalada", ["soup"] = "sopa", ["steak"] = "filete",
        ["sushi"] = "sushi", ["taco"] = "taco", ["burrito"] = "burrito",
        ["cake"] = "tarta", ["cookie"] = "galleta", ["chocolate"] = "chocolate",
        ["ice cream"] = "helado", ["donut"] = "donut",
        ["coffee"] = "café", ["tea"] = "té", ["juice"] = "zumo",
        ["almonds"] = "almendras", ["walnuts"] = "nueces", ["peanut"] = "cacahuete",
        ["oatmeal"] = "avena", ["cereal"] = "cereales",
        ["avocado"] = "aguacate", ["beans"] = "judías", ["lentils"] = "lentejas",
    };

    public FoodRecognitionService(HttpClient http) => _http = http;

    public async Task<List<RecognizedFood>> RecognizeAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            var base64 = Convert.ToBase64String(imageBytes);
            var requestBody = new
            {
                user_app_id = new { user_id = "clarifai", app_id = "main" },
                inputs = new[]
                {
                    new
                    {
                        data = new
                        {
                            image = new { base64 = base64 }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, ModelUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Key", ClarifaiPat);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _http.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
                return GetFallbackRecognition();

            var result = await response.Content.ReadFromJsonAsync<ClarifaiResponse>();
            if (result?.Outputs == null || result.Outputs.Count == 0)
                return GetFallbackRecognition();

            var concepts = result.Outputs[0]?.Data?.Concepts;
            if (concepts == null || concepts.Count == 0)
                return GetFallbackRecognition();

            return concepts
                .Where(c => c.Value >= 0.15) // solo con ≥15% de confianza
                .OrderByDescending(c => c.Value)
                .Take(8)
                .Select(c => new RecognizedFood(TranslateName(c.Name ?? ""), c.Value))
                .Where(r => !string.IsNullOrEmpty(r.Name))
                .ToList();
        }
        catch
        {
            return GetFallbackRecognition();
        }
    }

    private static string TranslateName(string name)
    {
        if (EnToEs.TryGetValue(name, out var es)) return es;
        // Capitalizar primera letra
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
    }

    private static List<RecognizedFood> GetFallbackRecognition() => new();

    // ─── DTOs Clarifai ───────────────────────────────────────────────────────

    private class ClarifaiResponse
    {
        [JsonPropertyName("outputs")]
        public List<ClarifaiOutput>? Outputs { get; set; }
    }

    private class ClarifaiOutput
    {
        [JsonPropertyName("data")]
        public ClarifaiData? Data { get; set; }
    }

    private class ClarifaiData
    {
        [JsonPropertyName("concepts")]
        public List<ClarifaiConcept>? Concepts { get; set; }
    }

    private class ClarifaiConcept
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }
}

