using System.Globalization;
using System.Text;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Contracts;

namespace CleanArchitecture.Infrastructure.Export;

/// <summary>UTF-8-CSV mit Kopfzeile, Trennzeichen <c>;</c> und ISO-8601-Zeitstempeln (UTC). Enthält nur Zahlen und feste Kategorienamen.</summary>
public sealed class CsvExportWriter : IExportWriter
{
    public string Format => ExportQuery.Csv;
    public string ContentType => "text/csv";
    public string FileExtension => "csv";

    public async Task WriteAsync(ExportData data, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
        await writer.WriteLineAsync("Id;DateTimeUtc;Systolisch;Diastolisch;Pulse;Oxygen;Category");
        foreach (var item in data.Items)
        {
            var line = string.Create(CultureInfo.InvariantCulture,
                $"{item.Id};{item.DateTime.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ};{item.Systolisch};{item.Diastolisch};{item.Pulse};{item.Oxygen};{item.Category}");
            await writer.WriteLineAsync(line);
        }
    }
}
