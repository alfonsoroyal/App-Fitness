namespace AppFitness.Shared.Models;

public class MealEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public MealType MealType { get; set; } = MealType.Almuerzo;
    public List<FoodItem> Foods { get; set; } = new();
    public string? Notes { get; set; }

    public double TotalKcal => Foods.Sum(f => f.TotalKcal);
    public double TotalProtein => Foods.Sum(f => f.TotalProtein);
    public double TotalCarbs => Foods.Sum(f => f.TotalCarbs);
    public double TotalFat => Foods.Sum(f => f.TotalFat);
}

public enum MealType
{
    Desayuno,
    Almuerzo,
    Cena,
    Snack
}

