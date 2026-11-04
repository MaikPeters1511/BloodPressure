using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Erinnerungen des angemeldeten Nutzers.</summary>
public interface IReminderApi
{
    [Get("/api/reminders")]
    Task<ReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Ersetzt alle Erinnerungen (höchstens 5).</summary>
    [Put("/api/reminders")]
    Task<ReminderSettingsDto> PutAsync([Body] ReminderSettingsDto settings, CancellationToken cancellationToken = default);
}

/// <summary>Benachrichtigungen des angemeldeten Nutzers.</summary>
public interface INotificationApi
{
    [Get("/api/notifications")]
    Task<IReadOnlyList<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    [Post("/api/notifications/{id}/read")]
    Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Typisierter Client für <c>/api/BloodPressure</c>. Fehlerantworten (400/404) werden als <see cref="ApiException"/> geworfen.</summary>
public interface IBloodPressureApi
{
    /// <summary>Paginierte Liste; <paramref name="from"/>/<paramref name="to"/> inklusive (UTC), Standard-Sortierung neueste zuerst.</summary>
    [Get("/api/BloodPressure")]
    Task<PagedResult<BloodPressureDto>> GetAllAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    /// <summary>Durchschnitt, Minimum, Maximum und Anzahl je Tag, Woche (Montag) oder Monat; Abschnitte ohne Messung fehlen.</summary>
    [Get("/api/BloodPressure/statistics")]
    Task<IReadOnlyList<StatisticsBucketDto>> GetStatisticsAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        string? interval = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lädt die Messungen des Zeitraums als Datei (<c>csv</c> oder <c>pdf</c>); höchstens 10 000 Messungen.</summary>
    [Get("/api/BloodPressure/export")]
    Task<Stream> ExportAsync(
        string? format = null,
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        CancellationToken cancellationToken = default);

    [Get("/api/BloodPressure/{id}")]
    Task<BloodPressureDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    [Post("/api/BloodPressure")]
    Task<BloodPressureDto> CreateAsync([Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Put("/api/BloodPressure/{id}")]
    Task UpdateAsync(int id, [Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/BloodPressure/{id}")]
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
