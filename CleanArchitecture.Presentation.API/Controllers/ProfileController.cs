using CleanArchitecture.Application.Profiles;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

/// <summary>Persönliche Angaben (Alter, Größe, Gewicht) des angemeldeten Nutzers. Gesundheitsdaten: nie in Logs oder Exporten.</summary>
[Route("api/v1/profile")]
[ApiController]
[Authorize]
public class ProfileController(IMessageBus bus) : AuthorizedControllerBase
{
    /// <summary>Das Profil; leer (alle Angaben <c>null</c>), wenn noch nichts gespeichert ist.</summary>
    [HttpGet]
    public async Task<ProfileDto> Get(CancellationToken ct)
        => await bus.InvokeAsync<ProfileDto>(new GetProfile(UserId), ct);

    /// <summary>Ersetzt das Profil. Alter 0–120, Größe 50–250 cm, Gewicht 2–500 kg (eine Nachkommastelle); jede Angabe optional.</summary>
    [HttpPut]
    public async Task<ProfileDto> Put(ProfileDto profile, CancellationToken ct)
        => await bus.InvokeAsync<ProfileDto>(new SaveProfile(UserId, profile), ct);

    /// <summary>Löscht alle Profilangaben.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteProfile(UserId), ct);
        return NoContent();
    }
}
