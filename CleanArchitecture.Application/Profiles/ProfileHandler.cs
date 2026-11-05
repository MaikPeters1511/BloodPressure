using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.Profiles;

public sealed record GetProfile(string UserId);
public sealed record SaveProfile(string UserId, ProfileDto Profile);
public sealed record DeleteProfile(string UserId);

public static class ProfileHandler
{
    public static async Task<ProfileDto> Handle(GetProfile query, IUserProfileRepository repository, TimeProvider time, CancellationToken ct)
        => ToDto(await repository.GetAsync(query.UserId, ct), time);

    public static async Task<ProfileDto> Handle(SaveProfile command, IUserProfileRepository repository, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var profile = await repository.GetAsync(command.UserId, ct) ?? new UserProfile { UserId = command.UserId };
        profile.Apply(command.Profile.Age, command.Profile.HeightCm, command.Profile.WeightKg, now);
        await repository.SaveAsync(profile, ct);
        return ToDto(profile, time);
    }

    public static async Task Handle(DeleteProfile command, IUserProfileRepository repository, CancellationToken ct)
        => await repository.DeleteAsync(command.UserId, ct);

    private static ProfileDto ToDto(UserProfile? profile, TimeProvider time)
        => profile is null
            ? new ProfileDto(null, null, null)
            : new ProfileDto(profile.AgeAt(time.GetUtcNow().UtcDateTime), profile.HeightCm, profile.WeightKg);
}
