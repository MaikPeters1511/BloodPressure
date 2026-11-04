using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CleanArchitecture.Presentation.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous] // Auth ist noch nicht eingerichtet (siehe sec-agent); bewusst explizit anonym.
public class BloodPressureController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<BloodPressureDto>> GetBloodPressures(CancellationToken ct)
        => await bus.InvokeAsync<IReadOnlyList<BloodPressureDto>>(new GetBloodPressures(), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BloodPressureDto>> GetBloodPressure(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<BloodPressureDto?>(new GetBloodPressureById(id), ct);
        return result is null ? NotFound() : result;
    }

    [HttpPost]
    public async Task<ActionResult<BloodPressureDto>> CreateBloodPressure(BloodPressureRequest request, CancellationToken ct)
    {
        var created = await bus.InvokeAsync<BloodPressureDto>(
            new CreateBloodPressure(request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen), ct);
        return CreatedAtAction(nameof(GetBloodPressure), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBloodPressure(int id, BloodPressureRequest request, CancellationToken ct)
    {
        var found = await bus.InvokeAsync<bool>(
            new UpdateBloodPressure(id, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBloodPressure(int id, CancellationToken ct)
    {
        var found = await bus.InvokeAsync<bool>(new DeleteBloodPressure(id), ct);
        return found ? NoContent() : NotFound();
    }
}
