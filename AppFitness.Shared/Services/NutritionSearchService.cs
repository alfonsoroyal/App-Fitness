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
            var url = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&search_simple=1&action=process&json=1&page_size=10&fields=product_name,nutriments,image_url";
            var response = await _http.GetFromJsonAsync<OpenFoodFactsResponse>(url);

            if (response?.Products == null) return new();

            return response.Products
                .Where(p => !string.IsNullOrWhiteSpace(p.ProductName))
                .Select(p => new FoodItem
                {
                    Name = p.ProductName!,
                    KcalPer100g = p.Nutriments?.EnergyKcal100g ?? 0,
                    ProteinPer100g = p.Nutriments?.Proteins100g ?? 0,
                    CarbsPer100g = p.Nutriments?.Carbohydrates100g ?? 0,
                    FatPer100g = p.Nutriments?.Fat100g ?? 0,
                    ImageUrl = p.ImageUrl,
                    QuantityGrams = 100
                })
                .Take(8)
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    // DTOs para Open Food Facts
    private class OpenFoodFactsResponse
    {
        [JsonPropertyName("products")]
        public List<OFFProduct>? Products { get; set; }
    }

    private class OFFProduct
    {
        [JsonPropertyName("product_name")]
        public string? ProductName { get; set; }

        [JsonPropertyName("image_url")]
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

