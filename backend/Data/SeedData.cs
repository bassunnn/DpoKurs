using HealthyHabits.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthyHabits.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(HabitsDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Habits.AnyAsync())
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var habits = new[]
        {
            new Habit
            {
                Title = "Выпить 2 литра воды",
                Category = "Питание",
                Color = "#1f7a8c",
                Icon = "droplets",
                TargetPerWeek = 7,
                ReminderTime = new TimeOnly(10, 0),
                CheckIns = BuildCheckIns(today, [0, -1, -2, -4, -5])
            },
            new Habit
            {
                Title = "30 минут ходьбы",
                Category = "Активность",
                Color = "#d97706",
                Icon = "footprints",
                TargetPerWeek = 5,
                ReminderTime = new TimeOnly(19, 30),
                CheckIns = BuildCheckIns(today, [-1, -2, -3, -6])
            },
            new Habit
            {
                Title = "Сон до 23:30",
                Category = "Восстановление",
                Color = "#4f46e5",
                Icon = "moon",
                TargetPerWeek = 6,
                ReminderTime = new TimeOnly(22, 45),
                CheckIns = BuildCheckIns(today, [0, -2, -3, -4, -5, -6])
            }
        };

        db.Habits.AddRange(habits);
        await db.SaveChangesAsync();
    }

    private static List<HabitCheckIn> BuildCheckIns(DateOnly today, int[] dayOffsets)
    {
        return dayOffsets
            .Select(offset => new HabitCheckIn { Date = today.AddDays(offset) })
            .ToList();
    }
}
