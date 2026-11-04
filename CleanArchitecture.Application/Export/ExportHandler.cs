using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.Export;

public sealed record ExportBloodPressures(string UserId, ExportQuery Query);

public static class ExportHandler
{
    public static async Task<ExportFile> Handle(
        ExportBloodPressures command,
        IBloodPressureRepository repository,
        IEnumerable<IExportWriter> writers,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var writer = writers.Single(w => string.Equals(w.Format, command.Query.Format, StringComparison.OrdinalIgnoreCase));
        var from = BloodPressureHandler.ToUtc(command.Query.From);
        var to = BloodPressureHandler.ToUtc(command.Query.To);

        // TotalCount zählt alle Treffer; geladen wird höchstens die erlaubte Menge.
        var page = await repository.GetPageAsync(
            command.UserId, new BloodPressureFilter(from, to, 1, ExportQuery.MaxMeasurements, Descending: false), ct);
        if (page.TotalCount > ExportQuery.MaxMeasurements)
        {
            throw new ExportLimitExceededException();
        }

        var data = new ExportData(from, to, page.Items.Select(BloodPressureMapping.ToDto).ToList());
        using var stream = new MemoryStream();
        await writer.WriteAsync(data, stream, ct);

        var fileName = $"bloodpressure-{timeProvider.GetUtcNow():yyyyMMdd}.{writer.FileExtension}";
        return new ExportFile(stream.ToArray(), writer.ContentType, fileName);
    }
}
