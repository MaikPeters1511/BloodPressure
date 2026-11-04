using CleanArchitecture.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Persistence.PostgreSql;

/// <summary>Datenbankkontext für Fachdaten und Microsoft Identity (Benutzer, Rollen).</summary>
public class AddPostgresSqlDbContext(DbContextOptions<AddPostgresSqlDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<BloodPressure> BloodPressures => Set<BloodPressure>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BloodPressure>(entity =>
        {
            entity.ToTable("BloodPressure");
            entity.HasKey(e => e.Id);
            // Backing-Felder werden von EF über die Properties befüllt (Validierung im Setter).
            entity.Property(e => e.Systolisch);
            entity.Property(e => e.Diastolisch);
            entity.Property(e => e.Pulse);
            entity.Property(e => e.Oxygen);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Version).IsRowVersion();
            entity.HasIndex(e => new { e.UserId, e.DateTime });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Message).HasMaxLength(500);
            // Wird die Messung gelöscht, bleibt der Hinweis ohne Verweis bestehen.
            entity.HasOne(e => e.BloodPressure).WithMany().HasForeignKey(e => e.BloodPressureId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.UserId, e.ReadAt, e.CreatedAt });
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.ToTable("Reminder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.TimeZoneId).HasMaxLength(100);
            entity.HasIndex(e => new { e.UserId, e.TimeOfDay }).IsUnique();
        });
    }
}
