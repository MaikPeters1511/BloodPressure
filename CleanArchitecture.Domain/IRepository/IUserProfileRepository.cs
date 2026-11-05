using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

public interface IUserProfileRepository
{
    /// <summary>Das Profil des Nutzers oder <c>null</c>, wenn keines gespeichert ist.</summary>
    Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Legt das Profil an oder ersetzt es.</summary>
    Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
