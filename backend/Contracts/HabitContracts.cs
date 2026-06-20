namespace HealthyHabits.Api.Contracts;

public sealed record HabitResponse(
    Guid Id,
    string Title,
    string Category,
    string Color,
    string Icon,
    int TargetPerWeek,
    string? ReminderTime,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<DateOnly> CompletedDates,
    HabitStats Stats);

public sealed record HabitStats(
    int CompletedInRange,
    int CurrentStreak,
    int BestStreak,
    double CompletionRate);

public sealed record CreateHabitRequest(
    string Title,
    string Category,
    string Color,
    string Icon,
    int TargetPerWeek,
    string? ReminderTime);

public sealed record UpdateHabitRequest(
    string Title,
    string Category,
    string Color,
    string Icon,
    int TargetPerWeek,
    string? ReminderTime);

public sealed record ToggleCheckInRequest(DateOnly Date, string? Note);

public sealed record ToggleCheckInResponse(bool Completed, DateOnly Date);

public sealed record DashboardResponse(
    int ActiveHabits,
    int CompletedToday,
    int CompletedThisWeek,
    int CurrentBestStreak,
    double AverageCompletionRate,
    IReadOnlyCollection<DayProgressResponse> LastSevenDays);

public sealed record DayProgressResponse(DateOnly Date, int Completed, int Planned);
