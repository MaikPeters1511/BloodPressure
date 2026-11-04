using CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Persistence.PostgreSql;

public class AddPostgresSqlDbContext(DbContextOptions<AddPostgresSqlDbContext> options) : DbContext(options)
{
    public DbSet<BloodPressure> BloodPressures => Set<BloodPressure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BloodPressure>(entity =>
        {
            entity.ToTable("BloodPressure");
            entity.HasKey(e => e.Id);
            // Backing-Felder werden von EF über die Properties befüllt (Validierung im Setter).
            entity.Property(e => e.Systolisch);
            entity.Property(e => e.Diastolisch);
            entity.Property(e => e.Pulse);
            entity.Property(e => e.Oxygen);
        });
    }
}
