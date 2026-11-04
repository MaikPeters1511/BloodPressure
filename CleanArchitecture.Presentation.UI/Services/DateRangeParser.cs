using System.Globalization;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Wandelt lokale Datumseingaben (<c>yyyy-MM-dd</c>) in UTC-Grenzen um: Beginn des Starttags bis Ende des Endtags.</summary>
public static class DateRangeParser
{
    public const string Format = "yyyy-MM-dd";

    public enum Result { Ok, Invalid, FromAfterTo }

    public static Result TryParse(string? from, string? to, TimeZoneInfo zone, out DateTime? fromUtc, out DateTime? toUtc)
    {
        fromUtc = toUtc = null;
        DateTime? fromDay = null, toDay = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParseExact(from, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            {
                return Result.Invalid;
            }

            fromDay = f;
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParseExact(to, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            {
                return Result.Invalid;
            }

            toDay = t;
        }

        if (fromDay > toDay)
        {
            return Result.FromAfterTo;
        }

        fromUtc = fromDay is { } start ? ToUtc(start, zone) : null;
        // Ende des Endtags: Beginn des Folgetags minus eine Millisekunde (die API-Grenzen sind inklusive).
        toUtc = toDay is { } end ? ToUtc(end.AddDays(1), zone).AddMilliseconds(-1) : null;
        return Result.Ok;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
}
