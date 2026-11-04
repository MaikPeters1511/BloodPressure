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
    public static async Task<PagedResult<BloodPressureDto>> Handle(
        GetBloodPressures query, IBloodPressureRepository repository, CancellationToken ct)
    {
        // Zu große Seiten werden begrenzt statt abgelehnt.
        var pageSize = Math.Min(query.Query.PageSize, BloodPressureListQuery.MaxPageSize);
        var filter = new BloodPressureFilter(
            ToUtc(query.Query.From),
            ToUtc(query.Query.To),
            query.Query.Page,
            pageSize,
            Descending: !string.Equals(query.Query.Sort, BloodPressureListQuery.SortDateAscending, StringComparison.OrdinalIgnoreCase));

        var page = await repository.GetPageAsync(query.UserId, filter, ct);
        return new PagedResult<BloodPressureDto>(
            page.Items.Select(BloodPressureMapping.ToDto).ToList(), query.Query.Page, pageSize, page.TotalCount);
    }

    // npgsql erlaubt für timestamptz nur UTC; ohne Zeitzonenangabe wird UTC angenommen (Vertrag: alle Zeiten UTC).
    internal static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } => value,
        { Kind: DateTimeKind.Local } => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    public static async Task<BloodPressureDto?> Handle(
        GetBloodPressureById query, IBloodPressureRepository repository, CancellationToken ct)
    {
        var item = await repository.GetByIdAsync(query.Id, query.UserId, ct);
        return item is null ? null : BloodPressureMapping.ToDto(item);
    }

    public static async Task<BloodPressureDto> Handle(
        CreateBloodPressure command, IBloodPressureRepository repository, TimeProvider timeProvider, CancellationToken ct)
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
        // Kritische Werte erzeugen in derselben Transaktion eine Benachrichtigung (kein Verlust bei Abbruch).
        var notification = Notification.ForCriticalReading(entity, timeProvider.GetUtcNow().UtcDateTime);
        await repository.AddAsync(entity, notification, ct);
        return BloodPressureMapping.ToDto(entity);
    }

    /// <summary>Gibt die aktualisierte Messung zurück oder <c>null</c>, wenn sie nicht existiert.</summary>
    public static async Task<BloodPressureDto?> Handle(
        UpdateBloodPressure command, IBloodPressureRepository repository, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, command.UserId, ct);
        if (entity is null)
        {
            return null;
        }

        EnsureVersion(entity, command.ExpectedVersion);
        entity.Systolisch = command.Systolisch;
        entity.Diastolisch = command.Diastolisch;
        entity.DateTime = command.DateTime;
        entity.Pulse = command.Pulse;
        entity.Oxygen = command.Oxygen;
        await repository.UpdateAsync(entity, ct);
        return BloodPressureMapping.ToDto(entity);
    }

    private static void EnsureVersion(BloodPressure entity, uint? expectedVersion)
    {
        if (expectedVersion is { } expected && expected != entity.Version)
        {
            throw new PreconditionFailedException();
        }
    }

    public static async Task<bool> Handle(
        DeleteBloodPressure command, IBloodPressureRepository repository, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(command.Id, command.UserId, ct);
        if (entity is null)
        {
            return false;
        }

        EnsureVersion(entity, command.ExpectedVersion);
        await repository.DeleteAsync(entity, ct);
        return true;
    }
}
