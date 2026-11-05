using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class UserProfileRepository(AddPostgresSqlDbContext context) : IUserProfileRepository
{
    public async Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken = default)
        => await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        if (context.Entry(profile).State == EntityState.Detached)
        {
            context.UserProfiles.Add(profile);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
        => await context.UserProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
