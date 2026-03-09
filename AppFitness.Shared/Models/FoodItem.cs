namespace AppFitness.Shared.Models;

public class FoodItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public double QuantityGrams { get; set; } = 100;
    public double KcalPer100g { get; set; }
    public double ProteinPer100g { get; set; }
    public double CarbsPer100g { get; set; }
    public double FatPer100g { get; set; }
    public string? ImageUrl { get; set; }

    public double TotalKcal => Math.Round(KcalPer100g * QuantityGrams / 100, 1);
    public double TotalProtein => Math.Round(ProteinPer100g * QuantityGrams / 100, 1);
    public double TotalCarbs => Math.Round(CarbsPer100g * QuantityGrams / 100, 1);
    public double TotalFat => Math.Round(FatPer100g * QuantityGrams / 100, 1);
}

