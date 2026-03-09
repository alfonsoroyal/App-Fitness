namespace AppFitness.Shared.Models;

public class WorkoutSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public double WeightKg { get; set; }
    public int Reps { get; set; }
    public int Sets { get; set; } = 1;

    public double Volume => WeightKg * Reps * Sets;
}

public class WorkoutSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
    public List<WorkoutSet> Sets { get; set; } = new();

    public double TotalVolume => Sets.Sum(s => s.Volume);
    public int TotalSets => Sets.Sum(s => s.Sets);
    public string Duration { get; set; } = string.Empty;

    /// <summary>Calorías quemadas estimadas por la IA (0 si no se ha calculado).</summary>
    public double EstimatedKcalBurned { get; set; }
}

