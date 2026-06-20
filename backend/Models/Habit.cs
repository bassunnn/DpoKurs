namespace HealthyHabits.Api.Models;

public sealed class Habit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#2f7d6d";
    public string Icon { get; set; } = "leaf";
    public int TargetPerWeek { get; set; } = 5;
    public TimeOnly? ReminderTime { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
    public List<HabitCheckIn> CheckIns { get; set; } = [];
}
