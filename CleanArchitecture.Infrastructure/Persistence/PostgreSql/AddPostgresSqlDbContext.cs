using CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Persistence.PostgreSql;

public class AddPostgresSqlDbContext : DbContext
{
    public AddPostgresSqlDbContext(DbContextOptions<AddPostgresSqlDbContext> options): base (options)
    {
    }

    public DbSet<BloodPressure> BloodPressures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<BloodPressure>().ToTable("BloodPressure");
        modelBuilder.Entity<BloodPressure>().HasKey(s => s.Id);
    }
}