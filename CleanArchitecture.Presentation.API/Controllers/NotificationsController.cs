using CleanArchitecture.Application.Notifications;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController(IMessageBus bus) : AuthorizedControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> Get(CancellationToken ct)
        => await bus.InvokeAsync<IReadOnlyList<NotificationDto>>(new GetNotifications(UserId), ct);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
        => await bus.InvokeAsync<bool>(new MarkNotificationRead(UserId, id), ct) ? NoContent() : NotFound();
}
