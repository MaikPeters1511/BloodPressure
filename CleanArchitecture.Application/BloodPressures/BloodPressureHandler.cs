using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.BloodPressures;

/// <summary>
/// Wolverine-Handler (CQRS). Rückgabe <c>null</c> bedeutet "nicht gefunden";
/// ungültige Werte lösen <see cref="ArgumentOutOfRangeException"/> aus der Domain aus.
/// </summary>
public static class BloodPressureHandler
{
    public static async Task<IReadOnlyList<BloodPressureDto>> Handle(
        GetBloodPressures query, IBloodPressureRepository repository, CancellationToken ct)
    {
        var items = await repository.GetAllAsync(query.UserId, ct);
        return items.Select(BloodPressureMapping.ToDto).ToList();
    }

    public static async Task<BloodPressureDto?> Handle(
        GetBloodPressureById query, IBloodPressureRepository repository, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(query.Id, query.UserId, ct);
        return item is null ? null : BloodPressureMapping.ToDto(item);
    }

    public static async Task<BloodPressureDto> Handle(
        CreateBloodPressure command, IBloodPressureRepository repository, CancellationToken ct)
    {
        var entity = new BloodPressure
        {
            UserId = command.UserId,
            Systolisch = command.Systolisch,
            Diastolisch = command.Diastolisch,
            DateTime = command.DateTime,
            Pulse = command.Pulse,
            Oxygen = command.Oxygen
        };
        await repository.AddAsync(entity, ct);
        return BloodPressureMapping.ToDto(entity);
    }

    public static async Task<bool> Handle(
        UpdateBloodPressure command, IBloodPressureRepository repository, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, command.UserId, ct);
        if (entity is null)
        {
            return false;
        }

        entity.Systolisch = command.Systolisch;
        entity.Diastolisch = command.Diastolisch;
        entity.DateTime = command.DateTime;
        entity.Pulse = command.Pulse;
        entity.Oxygen = command.Oxygen;
        await repository.UpdateAsync(entity, ct);
        return true;
    }

    public static async Task<bool> Handle(
        DeleteBloodPressure command, IBloodPressureRepository repository, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, command.UserId, ct);
        if (entity is null)
        {
            return false;
        }

        await repository.DeleteAsync(entity, ct);
        return true;
    }
}
