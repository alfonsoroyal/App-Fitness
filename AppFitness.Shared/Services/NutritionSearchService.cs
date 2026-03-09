using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AppFitness.Shared.Models;

namespace AppFitness.Shared.Services;

public class NutritionSearchService : INutritionSearchService
{
    private readonly HttpClient _http;

    public NutritionSearchService(HttpClient http) => _http = http;

    public async Task<List<FoodItem>> SearchFoodAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        try
        {
            // Usamos la API V2 de Open Food Facts con HTTPS obligatorio para evitar
            // bloqueos de mixed-content en GitHub Pages (HTTPS)
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://world.openfoodfacts.org/api/v2/search" +
                      $"?search_terms={encodedQuery}" +
                      $"&fields=product_name,nutriments,image_front_small_url" +
                      $"&page_size=10" +
                      $"&json=1";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _http.GetFromJsonAsync<OpenFoodFactsV2Response>(url, cts.Token);

            if (response?.Products == null) return GetFallbackResults(query);

            var results = response.Products
                .Where(p => !string.IsNullOrWhiteSpace(p.ProductName))
                .Select(p => new FoodItem
                {
                    Name = p.ProductName!,
                    KcalPer100g = p.Nutriments?.EnergyKcal100g ?? 0,
                    ProteinPer100g = p.Nutriments?.Proteins100g ?? 0,
                    CarbsPer100g = p.Nutriments?.Carbohydrates100g ?? 0,
                    FatPer100g = p.Nutriments?.Fat100g ?? 0,
                    // Forzar HTTPS para evitar mixed-content en GitHub Pages
                    ImageUrl = SanitizeImageUrl(p.ImageUrl),
                    QuantityGrams = 100
                })
                .Take(8)
                .ToList();

            return results.Any() ? results : GetFallbackResults(query);
        }
        catch
        {
            // Si falla la API (CORS en algunos navegadores, sin conexión, timeout)
            // devolvemos resultados del catálogo local de respaldo
            return GetFallbackResults(query);
        }
    }

    /// <summary>
    /// Fuerza HTTPS en URLs de imagen para evitar mixed-content bloqueado por el navegador.
    /// </summary>
    private static string? SanitizeImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[7..];
        return url;
    }

    /// <summary>
    /// Catálogo local de alimentos comunes como fallback cuando la API no está disponible.
    /// </summary>
    private static List<FoodItem> GetFallbackResults(string query)
    {
        var catalog = GetLocalFoodCatalog();
        var q = query.ToLowerInvariant();
        return catalog
            .Where(f => f.Name.ToLowerInvariant().Contains(q))
            .Take(8)
            .ToList();
    }

    public static List<FoodItem> GetLocalFoodCatalog() => new()
    {
        new() { Name = "Pollo a la plancha", KcalPer100g = 165, ProteinPer100g = 31, CarbsPer100g = 0, FatPer100g = 3.6, QuantityGrams = 100 },
        new() { Name = "Pechuga de pavo", KcalPer100g = 135, ProteinPer100g = 29, CarbsPer100g = 0, FatPer100g = 1.5, QuantityGrams = 100 },
        new() { Name = "Arroz blanco cocido", KcalPer100g = 130, ProteinPer100g = 2.7, CarbsPer100g = 28, FatPer100g = 0.3, QuantityGrams = 100 },
        new() { Name = "Arroz integral cocido", KcalPer100g = 111, ProteinPer100g = 2.6, CarbsPer100g = 23, FatPer100g = 0.9, QuantityGrams = 100 },
        new() { Name = "Avena (copos)", KcalPer100g = 389, ProteinPer100g = 17, CarbsPer100g = 66, FatPer100g = 7, QuantityGrams = 100 },
        new() { Name = "Huevo entero", KcalPer100g = 155, ProteinPer100g = 13, CarbsPer100g = 1.1, FatPer100g = 11, QuantityGrams = 100 },
        new() { Name = "Clara de huevo", KcalPer100g = 52, ProteinPer100g = 11, CarbsPer100g = 0.7, FatPer100g = 0.2, QuantityGrams = 100 },
        new() { Name = "Atún en lata (agua)", KcalPer100g = 116, ProteinPer100g = 26, CarbsPer100g = 0, FatPer100g = 1, QuantityGrams = 100 },
        new() { Name = "Salmón", KcalPer100g = 208, ProteinPer100g = 20, CarbsPer100g = 0, FatPer100g = 13, QuantityGrams = 100 },
        new() { Name = "Ternera magra", KcalPer100g = 250, ProteinPer100g = 26, CarbsPer100g = 0, FatPer100g = 15, QuantityGrams = 100 },
        new() { Name = "Leche entera", KcalPer100g = 61, ProteinPer100g = 3.2, CarbsPer100g = 4.8, FatPer100g = 3.3, QuantityGrams = 100 },
        new() { Name = "Leche desnatada", KcalPer100g = 34, ProteinPer100g = 3.4, CarbsPer100g = 4.8, FatPer100g = 0.1, QuantityGrams = 100 },
        new() { Name = "Yogur natural (0%)", KcalPer100g = 56, ProteinPer100g = 10, CarbsPer100g = 4, FatPer100g = 0.2, QuantityGrams = 100 },
        new() { Name = "Queso cottage", KcalPer100g = 98, ProteinPer100g = 11, CarbsPer100g = 3.4, FatPer100g = 4.3, QuantityGrams = 100 },
        new() { Name = "Requesón", KcalPer100g = 74, ProteinPer100g = 12, CarbsPer100g = 4, FatPer100g = 0.5, QuantityGrams = 100 },
        new() { Name = "Pan integral", KcalPer100g = 247, ProteinPer100g = 9, CarbsPer100g = 41, FatPer100g = 3.4, QuantityGrams = 100 },
        new() { Name = "Pan blanco", KcalPer100g = 265, ProteinPer100g = 9, CarbsPer100g = 49, FatPer100g = 3.2, QuantityGrams = 100 },
        new() { Name = "Pasta cocida", KcalPer100g = 131, ProteinPer100g = 5, CarbsPer100g = 25, FatPer100g = 1.1, QuantityGrams = 100 },
        new() { Name = "Patata cocida", KcalPer100g = 87, ProteinPer100g = 1.9, CarbsPer100g = 20, FatPer100g = 0.1, QuantityGrams = 100 },
        new() { Name = "Batata / boniato", KcalPer100g = 86, ProteinPer100g = 1.6, CarbsPer100g = 20, FatPer100g = 0.1, QuantityGrams = 100 },
        new() { Name = "Plátano", KcalPer100g = 89, ProteinPer100g = 1.1, CarbsPer100g = 23, FatPer100g = 0.3, QuantityGrams = 100 },
        new() { Name = "Manzana", KcalPer100g = 52, ProteinPer100g = 0.3, CarbsPer100g = 14, FatPer100g = 0.2, QuantityGrams = 100 },
        new() { Name = "Naranja", KcalPer100g = 47, ProteinPer100g = 0.9, CarbsPer100g = 12, FatPer100g = 0.1, QuantityGrams = 100 },
        new() { Name = "Fresa", KcalPer100g = 32, ProteinPer100g = 0.7, CarbsPer100g = 7.7, FatPer100g = 0.3, QuantityGrams = 100 },
        new() { Name = "Brócoli", KcalPer100g = 34, ProteinPer100g = 2.8, CarbsPer100g = 7, FatPer100g = 0.4, QuantityGrams = 100 },
        new() { Name = "Espinacas", KcalPer100g = 23, ProteinPer100g = 2.9, CarbsPer100g = 3.6, FatPer100g = 0.4, QuantityGrams = 100 },
        new() { Name = "Lechuga", KcalPer100g = 15, ProteinPer100g = 1.4, CarbsPer100g = 2.9, FatPer100g = 0.2, QuantityGrams = 100 },
        new() { Name = "Tomate", KcalPer100g = 18, ProteinPer100g = 0.9, CarbsPer100g = 3.9, FatPer100g = 0.2, QuantityGrams = 100 },
        new() { Name = "Aceite de oliva", KcalPer100g = 884, ProteinPer100g = 0, CarbsPer100g = 0, FatPer100g = 100, QuantityGrams = 100 },
        new() { Name = "Almendras", KcalPer100g = 579, ProteinPer100g = 21, CarbsPer100g = 22, FatPer100g = 50, QuantityGrams = 100 },
        new() { Name = "Whey Protein (polvo)", KcalPer100g = 400, ProteinPer100g = 80, CarbsPer100g = 8, FatPer100g = 5, QuantityGrams = 100 },
        new() { Name = "Creatina (polvo)", KcalPer100g = 0, ProteinPer100g = 0, CarbsPer100g = 0, FatPer100g = 0, QuantityGrams = 5 },
        new() { Name = "Legumbres cocidas (lentejas)", KcalPer100g = 116, ProteinPer100g = 9, CarbsPer100g = 20, FatPer100g = 0.4, QuantityGrams = 100 },
        new() { Name = "Garbanzos cocidos", KcalPer100g = 164, ProteinPer100g = 8.9, CarbsPer100g = 27, FatPer100g = 2.6, QuantityGrams = 100 },
        new() { Name = "Tofu", KcalPer100g = 76, ProteinPer100g = 8, CarbsPer100g = 1.9, FatPer100g = 4.8, QuantityGrams = 100 },
    };

    // DTOs para Open Food Facts API V2
    private class OpenFoodFactsV2Response
    {
        [JsonPropertyName("products")]
        public List<OFFProduct>? Products { get; set; }
    }

    private class OFFProduct
    {
        [JsonPropertyName("product_name")]
        public string? ProductName { get; set; }

        [JsonPropertyName("image_front_small_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("nutriments")]
        public OFFNutriments? Nutriments { get; set; }
    }

    private class OFFNutriments
    {
        [JsonPropertyName("energy-kcal_100g")]
        public double EnergyKcal100g { get; set; }

        [JsonPropertyName("proteins_100g")]
        public double Proteins100g { get; set; }

        [JsonPropertyName("carbohydrates_100g")]
        public double Carbohydrates100g { get; set; }

        [JsonPropertyName("fat_100g")]
        public double Fat100g { get; set; }
    }
}

