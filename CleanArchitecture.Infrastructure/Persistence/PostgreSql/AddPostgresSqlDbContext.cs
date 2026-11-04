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
            entity.HasIndex(e => e.UserId);
        });
    }
}
