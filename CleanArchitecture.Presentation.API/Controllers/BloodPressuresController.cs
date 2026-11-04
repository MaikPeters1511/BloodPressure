using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BloodPressureController(IMessageBus bus) : AuthorizedControllerBase
{
    [HttpGet]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC/ESH 2018 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")]
        public async Task<PagedResult<BloodPressureDto>> GetBloodPressures([FromQuery] BloodPressureListQuery query, CancellationToken ct)
        => await bus.InvokeAsync<PagedResult<BloodPressureDto>>(new GetBloodPressures(UserId, query), ct);

    [HttpGet("statistics")]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC/ESH 2018 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")]
        public async Task<IReadOnlyList<StatisticsBucketDto>> GetStatistics([FromQuery] StatisticsQuery query, CancellationToken ct)
        => await bus.InvokeAsync<IReadOnlyList<StatisticsBucketDto>>(new GetStatistics(UserId, query), ct);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ExportQuery query, CancellationToken ct)
    {
        var file = await bus.InvokeAsync<ExportFile>(new ExportBloodPressures(UserId, query), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:int}")]
    [EndpointDescription("Die Kategorie ist eine Einordnung nach ESC/ESH 2018 und keine Diagnose. Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")]
        public async Task<ActionResult<BloodPressureDto>> GetBloodPressure(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<BloodPressureDto?>(new GetBloodPressureById(UserId, id), ct);
        return result is null ? NotFound() : result;
    }

    [HttpPost]
    public async Task<ActionResult<BloodPressureDto>> CreateBloodPressure(BloodPressureRequest request, CancellationToken ct)
    {
        var created = await bus.InvokeAsync<BloodPressureDto>(
            new CreateBloodPressure(UserId, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen), ct);
        return CreatedAtAction(nameof(GetBloodPressure), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBloodPressure(int id, BloodPressureRequest request, CancellationToken ct)
    {
        var found = await bus.InvokeAsync<bool>(
            new UpdateBloodPressure(UserId, id, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBloodPressure(int id, CancellationToken ct)
    {
        var found = await bus.InvokeAsync<bool>(new DeleteBloodPressure(UserId, id), ct);
        return found ? NoContent() : NotFound();
    }
}
