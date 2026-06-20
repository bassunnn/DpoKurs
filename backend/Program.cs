using HealthyHabits.Api.Contracts;
using HealthyHabits.Api.Data;
using HealthyHabits.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=healthy_habits;Username=postgres;Password=postgres";

builder.Services.AddDbContext<HabitsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HabitsDbContext>();
    await SeedData.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "Healthy Habits" }));

app.MapGet("/api/habits", async (HabitsDbContext db, DateOnly? from, DateOnly? to) =>
{
    var range = ResolveRange(from, to);
    var habits = await db.Habits
        .AsNoTracking()
        .Include(habit => habit.CheckIns)
        .Where(habit => !habit.IsArchived)
        .OrderBy(habit => habit.CreatedAtUtc)
        .ToListAsync();

    return Results.Ok(habits.Select(habit => ToResponse(habit, range.From, range.To)));
});

app.MapGet("/api/habits/{id:guid}", async (Guid id, HabitsDbContext db, DateOnly? from, DateOnly? to) =>
{
    var habit = await db.Habits
        .AsNoTracking()
        .Include(item => item.CheckIns)
        .FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);

    if (habit is null)
    {
        return Results.NotFound();
    }

    var range = ResolveRange(from, to);
    return Results.Ok(ToResponse(habit, range.From, range.To));
});

app.MapPost("/api/habits", async (CreateHabitRequest request, HabitsDbContext db) =>
{
    var validation = ValidateHabit(request.Title, request.Category, request.Color, request.Icon, request.TargetPerWeek, request.ReminderTime);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var habit = new Habit
    {
        Title = request.Title.Trim(),
        Category = request.Category.Trim(),
        Color = request.Color.Trim(),
        Icon = request.Icon.Trim(),
        TargetPerWeek = request.TargetPerWeek,
        ReminderTime = ParseTime(request.ReminderTime)
    };

    db.Habits.Add(habit);
    await db.SaveChangesAsync();

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var response = ToResponse(habit, today.AddDays(-6), today);
    return Results.Created($"/api/habits/{habit.Id}", response);
});

app.MapPut("/api/habits/{id:guid}", async (Guid id, UpdateHabitRequest request, HabitsDbContext db) =>
{
    var habit = await db.Habits.Include(item => item.CheckIns).FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
    if (habit is null)
    {
        return Results.NotFound();
    }

    var validation = ValidateHabit(request.Title, request.Category, request.Color, request.Icon, request.TargetPerWeek, request.ReminderTime);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    habit.Title = request.Title.Trim();
    habit.Category = request.Category.Trim();
    habit.Color = request.Color.Trim();
    habit.Icon = request.Icon.Trim();
    habit.TargetPerWeek = request.TargetPerWeek;
    habit.ReminderTime = ParseTime(request.ReminderTime);

    await db.SaveChangesAsync();

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    return Results.Ok(ToResponse(habit, today.AddDays(-6), today));
});

app.MapDelete("/api/habits/{id:guid}", async (Guid id, HabitsDbContext db) =>
{
    var habit = await db.Habits.FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
    if (habit is null)
    {
        return Results.NotFound();
    }

    habit.IsArchived = true;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/habits/{id:guid}/checkins/toggle", async (Guid id, ToggleCheckInRequest request, HabitsDbContext db) =>
{
    var habitExists = await db.Habits.AnyAsync(item => item.Id == id && !item.IsArchived);
    if (!habitExists)
    {
        return Results.NotFound();
    }

    var existing = await db.HabitCheckIns
        .FirstOrDefaultAsync(item => item.HabitId == id && item.Date == request.Date);

    if (existing is null)
    {
        db.HabitCheckIns.Add(new HabitCheckIn
        {
            HabitId = id,
            Date = request.Date,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        });
        await db.SaveChangesAsync();
        return Results.Ok(new ToggleCheckInResponse(true, request.Date));
    }

    db.HabitCheckIns.Remove(existing);
    await db.SaveChangesAsync();
    return Results.Ok(new ToggleCheckInResponse(false, request.Date));
});

app.MapGet("/api/dashboard", async (HabitsDbContext db) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var weekStart = StartOfWeek(today);
    var lastSevenDaysStart = today.AddDays(-6);

    var habits = await db.Habits
        .AsNoTracking()
        .Include(habit => habit.CheckIns)
        .Where(habit => !habit.IsArchived)
        .ToListAsync();

    var activeHabits = habits.Count;
    var allDates = habits.SelectMany(habit => habit.CheckIns.Select(checkIn => checkIn.Date)).ToList();
    var completedToday = allDates.Count(date => date == today);
    var completedThisWeek = allDates.Count(date => date >= weekStart && date <= today);
    var currentBestStreak = habits.Count == 0 ? 0 : habits.Max(habit => CalculateCurrentStreak(habit.CheckIns.Select(item => item.Date).ToHashSet(), today));
    var averageCompletionRate = habits.Count == 0
        ? 0
        : habits.Average(habit => BuildStats(habit.CheckIns.Select(item => item.Date).ToHashSet(), lastSevenDaysStart, today).CompletionRate);

    var lastSevenDays = Enumerable.Range(0, 7)
        .Select(offset =>
        {
            var date = lastSevenDaysStart.AddDays(offset);
            return new DayProgressResponse(
                date,
                habits.Sum(habit => habit.CheckIns.Any(checkIn => checkIn.Date == date) ? 1 : 0),
                activeHabits);
        })
        .ToList();

    return Results.Ok(new DashboardResponse(
        activeHabits,
        completedToday,
        completedThisWeek,
        currentBestStreak,
        Math.Round(averageCompletionRate, 2),
        lastSevenDays));
});

app.Run();

static (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    return (from ?? today.AddDays(-6), to ?? today);
}

static HabitResponse ToResponse(Habit habit, DateOnly from, DateOnly to)
{
    var completedDates = habit.CheckIns
        .Select(checkIn => checkIn.Date)
        .Order()
        .ToList();

    return new HabitResponse(
        habit.Id,
        habit.Title,
        habit.Category,
        habit.Color,
        habit.Icon,
        habit.TargetPerWeek,
        habit.ReminderTime?.ToString("HH:mm"),
        habit.CreatedAtUtc,
        completedDates,
        BuildStats(completedDates.ToHashSet(), from, to));
}

static HabitStats BuildStats(HashSet<DateOnly> completedDates, DateOnly from, DateOnly to)
{
    var rangeLength = Math.Max(1, to.DayNumber - from.DayNumber + 1);
    var completedInRange = completedDates.Count(date => date >= from && date <= to);
    var currentStreak = CalculateCurrentStreak(completedDates, to);
    var bestStreak = CalculateBestStreak(completedDates);
    var completionRate = Math.Round((double)completedInRange / rangeLength * 100, 2);

    return new HabitStats(completedInRange, currentStreak, bestStreak, completionRate);
}

static int CalculateCurrentStreak(HashSet<DateOnly> dates, DateOnly fromDate)
{
    var streak = 0;
    var cursor = fromDate;

    while (dates.Contains(cursor))
    {
        streak++;
        cursor = cursor.AddDays(-1);
    }

    return streak;
}

static int CalculateBestStreak(HashSet<DateOnly> dates)
{
    var best = 0;
    var current = 0;
    DateOnly? previous = null;

    foreach (var date in dates.Order())
    {
        current = previous.HasValue && date.DayNumber == previous.Value.DayNumber + 1 ? current + 1 : 1;
        best = Math.Max(best, current);
        previous = date;
    }

    return best;
}

static DateOnly StartOfWeek(DateOnly date)
{
    var diff = ((int)date.DayOfWeek + 6) % 7;
    return date.AddDays(-diff);
}

static object? ValidateHabit(string title, string category, string color, string icon, int targetPerWeek, string? reminderTime)
{
    if (string.IsNullOrWhiteSpace(title) || title.Length > 120)
    {
        return new { error = "Название привычки должно быть от 1 до 120 символов." };
    }

    if (string.IsNullOrWhiteSpace(category) || category.Length > 60)
    {
        return new { error = "Категория должна быть от 1 до 60 символов." };
    }

    if (string.IsNullOrWhiteSpace(color) || color.Length > 16)
    {
        return new { error = "Цвет обязателен." };
    }

    if (string.IsNullOrWhiteSpace(icon) || icon.Length > 32)
    {
        return new { error = "Иконка обязательна." };
    }

    if (targetPerWeek is < 1 or > 7)
    {
        return new { error = "Цель в неделю должна быть от 1 до 7." };
    }

    if (!string.IsNullOrWhiteSpace(reminderTime) && ParseTime(reminderTime) is null)
    {
        return new { error = "Время напоминания должно быть в формате HH:mm." };
    }

    return null;
}

static TimeOnly? ParseTime(string? value)
{
    return TimeOnly.TryParse(value, out var parsed) ? parsed : null;
}
