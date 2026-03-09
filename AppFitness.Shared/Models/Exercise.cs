namespace AppFitness.Shared.Models;

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public ExerciseCategory Category { get; set; } = ExerciseCategory.Fuerza;
}

public enum ExerciseCategory
{
    Fuerza,
    Cardio,
    Flexibilidad,
    Otro
}

