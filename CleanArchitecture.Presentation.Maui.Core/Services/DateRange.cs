namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Wandelt lokale Kalendertage in inklusive UTC-Grenzen um: Beginn des Starttags bis Ende des Endtags.</summary>
public static class DateRange
{
    public static (DateTime? From, DateTime? To) ToUtcBounds(DateTime? fromDay, DateTime? toDay, TimeZoneInfo zone)
        => (fromDay is { } from ? ToUtc(from.Date, zone) : null,
            toDay is { } to ? ToUtc(to.Date.AddDays(1), zone).AddMilliseconds(-1) : null);

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
}
