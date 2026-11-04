using CleanArchitecture.Application.Reminders;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

[Route("api/reminders")]
[ApiController]
[Authorize]
public class RemindersController(IMessageBus bus) : AuthorizedControllerBase
{
    [HttpGet]
    public async Task<ReminderSettingsDto> Get(CancellationToken ct)
        => await bus.InvokeAsync<ReminderSettingsDto>(new GetReminders(UserId), ct);

    /// <summary>Ersetzt alle Erinnerungen des Nutzers (höchstens 5, eindeutige Uhrzeiten, IANA-Zeitzone).</summary>
    [HttpPut]
    public async Task<ReminderSettingsDto> Put(ReminderSettingsDto settings, CancellationToken ct)
        => await bus.InvokeAsync<ReminderSettingsDto>(new SetReminders(UserId, settings), ct);
}
