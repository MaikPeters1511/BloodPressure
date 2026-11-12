using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Erinnerungen des angemeldeten Nutzers.</summary>
public interface IReminderApi
{
    /// <summary>Zwei-Faktor-Anmeldung (TOTP) abfragen und verwalten; siehe <see cref="TwoFactorRequest"/>.</summary>
    [Post("/api/auth/manage/2fa")]
    Task<TwoFactorResponse> ManageTwoFactorAsync([Body] TwoFactorRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/reminders")]
    Task<ReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Ersetzt alle Erinnerungen (höchstens 5).</summary>
    [Put("/api/v1/reminders")]
    Task<ReminderSettingsDto> PutAsync([Body] ReminderSettingsDto settings, CancellationToken cancellationToken = default);
}

/// <summary>Benachrichtigungen des angemeldeten Nutzers.</summary>
public interface INotificationApi
{
    [Get("/api/v1/notifications")]
    Task<IReadOnlyList<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    [Post("/api/v1/notifications/{id}/read")]
    Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Typisierter Client für <c>/api/BloodPressure</c>. Fehlerantworten (400/404) werden als <see cref="ApiException"/> geworfen.</summary>
public interface IBloodPressureApi
{
    /// <summary>Paginierte Liste; <paramref name="from"/>/<paramref name="to"/> inklusive (UTC), Standard-Sortierung neueste zuerst.</summary>
    [Get("/api/v1/BloodPressure")]
    Task<PagedResult<BloodPressureDto>> GetAllAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    /// <summary>Durchschnitt, Minimum, Maximum und Anzahl je Tag, Woche (Montag) oder Monat; Abschnitte ohne Messung fehlen.</summary>
    [Get("/api/v1/BloodPressure/statistics")]
    Task<IReadOnlyList<StatisticsBucketDto>> GetStatisticsAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        string? interval = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lädt die Messungen des Zeitraums als Datei (<c>csv</c> oder <c>pdf</c>); höchstens 10 000 Messungen.</summary>
    [Get("/api/v1/BloodPressure/export")]
    Task<Stream> ExportAsync(
        string? format = null,
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        CancellationToken cancellationToken = default);

    [Get("/api/v1/BloodPressure/{id}")]
    Task<BloodPressureDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    [Post("/api/v1/BloodPressure")]
    Task<BloodPressureDto> CreateAsync([Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    /// <param name="ifMatch">Optional: <see cref="BloodPressureDto.ETag"/>; bei veraltetem Stand antwortet die API mit 412.</param>
    [Put("/api/v1/BloodPressure/{id}")]
    Task UpdateAsync(int id, [Body] BloodPressureRequest request, [Header("If-Match")] string? ifMatch = null, CancellationToken cancellationToken = default);

    /// <param name="ifMatch">Optional: <see cref="BloodPressureDto.ETag"/>; bei veraltetem Stand antwortet die API mit 412.</param>
    [Delete("/api/v1/BloodPressure/{id}")]
    Task DeleteAsync(int id, [Header("If-Match")] string? ifMatch = null, CancellationToken cancellationToken = default);
}
