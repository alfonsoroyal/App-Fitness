namespace AppFitness.Shared.Models;

public class UserProfile
{
    public string Name { get; set; } = "Usuario";
    public double WeightKg { get; set; } = 70;
    public double HeightCm { get; set; } = 175;
    public DateTime BirthDate { get; set; } = new DateTime(1990, 1, 1);
    public int DailyCalorieGoal { get; set; } = 2000;
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Moderado;
    public string Goal { get; set; } = "Mantener peso";

    public int Age => DateTime.Today.Year - BirthDate.Year -
                      (DateTime.Today.DayOfYear < BirthDate.DayOfYear ? 1 : 0);
}

public enum ActivityLevel
{
    Sedentario,
    Ligero,
    Moderado,
    Activo,
    MuyActivo
}

