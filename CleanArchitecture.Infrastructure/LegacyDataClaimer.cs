using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure;

/// <summary>
/// Altdaten gehören dem ersten registrierten Nutzer. Gibt es bereits mehrere Nutzer, bleiben die Altdaten
/// unverändert ohne Besitzer (nicht abrufbar), damit sie nie einem falschen Nutzer zugeordnet werden.
/// </summary>
public sealed class LegacyDataClaimer(AddPostgresSqlDbContext context) : ILegacyDataClaimer
{
    public async Task ClaimForFirstUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await context.Users.CountAsync(cancellationToken) != 1)
        {
            return;
        }

        await context.BloodPressures.Where(b => b.UserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.UserId, userId), cancellationToken);
    }
}
