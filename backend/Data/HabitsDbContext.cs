using HealthyHabits.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthyHabits.Api.Data;

public sealed class HabitsDbContext(DbContextOptions<HabitsDbContext> options) : DbContext(options)
{
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitCheckIn> HabitCheckIns => Set<HabitCheckIn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Habit>(entity =>
        {
            entity.HasKey(habit => habit.Id);
            entity.Property(habit => habit.Title).HasMaxLength(120).IsRequired();
            entity.Property(habit => habit.Category).HasMaxLength(60).IsRequired();
            entity.Property(habit => habit.Color).HasMaxLength(16).IsRequired();
            entity.Property(habit => habit.Icon).HasMaxLength(32).IsRequired();
            entity.Property(habit => habit.TargetPerWeek).HasDefaultValue(5);
            entity.HasMany(habit => habit.CheckIns)
                .WithOne(checkIn => checkIn.Habit)
                .HasForeignKey(checkIn => checkIn.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HabitCheckIn>(entity =>
        {
            entity.HasKey(checkIn => checkIn.Id);
            entity.Property(checkIn => checkIn.Note).HasMaxLength(300);
            entity.HasIndex(checkIn => new { checkIn.HabitId, checkIn.Date }).IsUnique();
        });
    }
}
