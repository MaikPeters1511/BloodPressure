using CleanArchitecture.Contracts;

namespace CleanArchitecture.Application.Export;

/// <summary>Daten eines Exports: Zeitraum und die (aufsteigend sortierten) Messungen des Nutzers.</summary>
public sealed record ExportData(DateTime? From, DateTime? To, IReadOnlyList<BloodPressureDto> Items);

/// <summary>Fertige Exportdatei.</summary>
public sealed record ExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Schreibt Exportdaten in ein Format. Implementierungen liegen in der Infrastruktur.</summary>
public interface IExportWriter
{
    /// <summary>Formatkennung wie in <see cref="ExportQuery.Format"/> (<c>csv</c>, <c>pdf</c>).</summary>
    string Format { get; }

    string ContentType { get; }

    string FileExtension { get; }

    Task WriteAsync(ExportData data, Stream output, CancellationToken cancellationToken);
}

/// <summary>Zu viele Messungen für einen Export; wird als 400 gemeldet.</summary>
public sealed class ExportLimitExceededException()
    : Exception($"Zu viele Messungen im Zeitraum (maximal {ExportQuery.MaxMeasurements}). Bitte den Zeitraum verkleinern.");
