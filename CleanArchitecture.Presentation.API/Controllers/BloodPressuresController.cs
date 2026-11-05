using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
[Authorize]
public class BloodPressureController(IMessageBus bus) : AuthorizedControllerBase
{
    [HttpGet]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC 2024 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")]
        public async Task<PagedResult<BloodPressureDto>> GetBloodPressures([FromQuery] BloodPressureListQuery query, CancellationToken ct)
        => await bus.InvokeAsync<PagedResult<BloodPressureDto>>(new GetBloodPressures(UserId, query), ct);

    [HttpGet("statistics")]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC 2024 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")]
        public async Task<IReadOnlyList<StatisticsBucketDto>> GetStatistics([FromQuery] StatisticsQuery query, CancellationToken ct)
        => await bus.InvokeAsync<IReadOnlyList<StatisticsBucketDto>>(new GetStatistics(UserId, query), ct);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ExportQuery query, CancellationToken ct)
    {
        var file = await bus.InvokeAsync<ExportFile>(new ExportBloodPressures(UserId, query), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC 2024 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen. Der Header ETag enthält die Version für If-Match bei PUT/DELETE.")]
    public async Task<ActionResult<BloodPressureDto>> GetBloodPressure(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<BloodPressureDto?>(new GetBloodPressureById(UserId, id), ct);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = result.ETag;
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<BloodPressureDto>> CreateBloodPressure(BloodPressureRequest request, CancellationToken ct)
    {
        var created = await bus.InvokeAsync<BloodPressureDto>(
            new CreateBloodPressure(UserId, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen), ct);
        return CreatedAtAction(nameof(GetBloodPressure), new { id = created.Id }, created);
    }

    /// <summary>Ändert eine Messung. Optional <c>If-Match</c> mit dem ETag aus GET; bei veraltetem Stand 412.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBloodPressure(int id, BloodPressureRequest request, CancellationToken ct)
    {
        if (!TryReadIfMatch(out var expectedVersion))
        {
            return InvalidIfMatch();
        }

        var updated = await bus.InvokeAsync<BloodPressureDto?>(
            new UpdateBloodPressure(UserId, id, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen, expectedVersion), ct);
        if (updated is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = updated.ETag;
        return NoContent();
    }

    /// <summary>Löscht eine Messung. Optional <c>If-Match</c> mit dem ETag aus GET; bei veraltetem Stand 412.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBloodPressure(int id, CancellationToken ct)
    {
        if (!TryReadIfMatch(out var expectedVersion))
        {
            return InvalidIfMatch();
        }

        var found = await bus.InvokeAsync<bool>(new DeleteBloodPressure(UserId, id, expectedVersion), ct);
        return found ? NoContent() : NotFound();
    }

    // If-Match fehlt oder "*": keine Prüfung. Sonst muss ein ETag der Form "123" vorliegen.
    private bool TryReadIfMatch(out uint? version)
    {
        version = null;
        var header = Request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0 || header == "*")
        {
            return true;
        }

        if (uint.TryParse(header.Trim('"'), out var parsed))
        {
            version = parsed;
            return true;
        }

        return false;
    }

    private ObjectResult InvalidIfMatch() => Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Ungültiger If-Match-Header",
        detail: "Erwartet wird der ETag-Wert aus der GET-Antwort, z. B. \"42\".");
}
